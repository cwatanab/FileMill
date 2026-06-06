using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileMill.Services;

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseUrl,
    string? PackageName,
    string? PackageUrl,
    bool IsUpdateAvailable);

public static class UpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/cwatanab/FileMill/releases/latest";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string CurrentVersionText => GetCurrentVersionText();

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.UserAgent.ParseAdd($"FileMill/{CurrentVersionText}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (release == null || string.IsNullOrWhiteSpace(release.TagName) || string.IsNullOrWhiteSpace(release.HtmlUrl))
            throw new InvalidOperationException("GitHub Release の情報を読み取れませんでした。");

        var currentVersion = ParseVersion(CurrentVersionText);
        var latestVersion = ParseVersion(release.TagName);
        var package = FindUpdatePackage(release);

        return new UpdateCheckResult(
            CurrentVersionText,
            NormalizeVersionText(release.TagName),
            string.IsNullOrWhiteSpace(release.Name) ? NormalizeVersionText(release.TagName) : release.Name,
            release.HtmlUrl,
            package?.Name,
            package?.BrowserDownloadUrl,
            latestVersion.CompareTo(currentVersion) > 0);
    }

    public static async Task<string> DownloadUpdatePackageAsync(UpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.PackageUrl))
            throw new InvalidOperationException("更新用 ZIP が GitHub Release に見つかりませんでした。");

        var packageName = string.IsNullOrWhiteSpace(update.PackageName)
            ? $"FileMill-{update.LatestVersion}.zip"
            : update.PackageName;
        var downloadDir = Path.Combine(Path.GetTempPath(), "FileMill", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(downloadDir);
        var packagePath = Path.Combine(downloadDir, packageName);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var request = new HttpRequestMessage(HttpMethod.Get, update.PackageUrl);
        request.Headers.UserAgent.ParseAdd($"FileMill/{CurrentVersionText}");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(packagePath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        return packagePath;
    }

    public static void StartUpdaterProcess(string packagePath)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("実行ファイルのパスを取得できませんでした。");

        var installDir = AppContext.BaseDirectory;
        var updaterDir = Path.Combine(Path.GetTempPath(), "FileMill", "updater", Guid.NewGuid().ToString("N"));
        CopyDirectory(installDir, updaterDir);

        var updaterPath = Path.Combine(updaterDir, Path.GetFileName(processPath));
        if (!File.Exists(updaterPath))
            throw new FileNotFoundException("updater の起動ファイルが見つかりません。", updaterPath);

        var startInfo = new ProcessStartInfo(updaterPath)
        {
            UseShellExecute = false,
            WorkingDirectory = updaterDir
        };
        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add(installDir);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(Path.GetFileName(processPath));

        Process.Start(startInfo);
    }

    public static void OpenReleasePage(string releaseUrl)
    {
        Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
    }

    public static bool TryRunUpdaterMode(string[] args)
    {
        if (args.Length == 0 || !args[0].Equals("--apply-update", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            ApplyUpdate(args);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"アップデートを適用できませんでした。\n\n{ex.Message}",
                "FileMill Updater",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return true;
    }

    private static void ApplyUpdate(string[] args)
    {
        if (args.Length < 5)
            throw new InvalidOperationException("updater の引数が不足しています。");

        var packagePath = args[1];
        var installDir = args[2];
        if (!int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentPid))
            throw new InvalidOperationException("親プロセス ID を解析できませんでした。");

        var restartFileName = args[4];

        WaitForParentExit(parentPid);

        var extractDir = Path.Combine(Path.GetTempPath(), "FileMill", "extract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractDir);

        ZipFile.ExtractToDirectory(packagePath, extractDir, overwriteFiles: true);
        var payloadDir = FindPayloadDirectory(extractDir, restartFileName);
        CopyDirectoryWithRetry(payloadDir, installDir);

        TryDeleteFile(packagePath);
        TryDeleteDirectory(extractDir);

        var restartPath = Path.Combine(installDir, restartFileName);
        if (File.Exists(restartPath))
        {
            Process.Start(new ProcessStartInfo(restartPath)
            {
                UseShellExecute = true,
                WorkingDirectory = installDir
            });
        }
    }

    private static Version ParseVersion(string value)
    {
        var normalized = NormalizeVersionText(value);
        if (!Version.TryParse(normalized, out var version))
            throw new InvalidOperationException($"バージョン番号を解析できませんでした: {value}");

        return new Version(
            version.Major,
            version.Minor,
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }

    private static string NormalizeVersionText(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        var metadataIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex >= 0)
            normalized = normalized[..metadataIndex];

        return normalized;
    }

    private static string GetCurrentVersionText()
    {
        var assembly = typeof(UpdateService).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
            return NormalizeVersionText(infoVersion);

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static GitHubAsset? FindUpdatePackage(GitHubRelease release)
    {
        if (release.Assets == null)
            return null;

        foreach (var asset in release.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Name) || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                continue;

            if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                && asset.Name.StartsWith("FileMill-", StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        return null;
    }

    private static void WaitForParentExit(int parentPid)
    {
        try
        {
            using var parent = Process.GetProcessById(parentPid);
            parent.WaitForExit(60_000);
        }
        catch (ArgumentException)
        {
        }
    }

    private static string FindPayloadDirectory(string extractDir, string restartFileName)
    {
        if (File.Exists(Path.Combine(extractDir, restartFileName)))
            return extractDir;

        foreach (var directory in Directory.GetDirectories(extractDir))
        {
            if (File.Exists(Path.Combine(directory, restartFileName)))
                return directory;
        }

        throw new FileNotFoundException("更新パッケージ内に FileMill の実行ファイルが見つかりません。", restartFileName);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var targetDirectory = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, directory));
            Directory.CreateDirectory(targetDirectory);
        }

        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var targetFile = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private static void CopyDirectoryWithRetry(string sourceDir, string targetDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var targetDirectory = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, directory));
            Directory.CreateDirectory(targetDirectory);
        }

        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var targetFile = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            CopyFileWithRetry(file, targetFile);
        }
    }

    private static void CopyFileWithRetry(string sourceFile, string targetFile)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                if (File.Exists(targetFile))
                    File.SetAttributes(targetFile, FileAttributes.Normal);

                File.Copy(sourceFile, targetFile, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(500);
            }
        }

        throw new IOException($"ファイルを更新できませんでした: {targetFile}", lastError);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
