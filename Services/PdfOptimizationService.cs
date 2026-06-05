using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileMill.Models;

namespace FileMill.Services;

public class PdfOptimizationService
{
    public async Task<long> OptimizeAsync(
        string inputPath,
        string outputPath,
        PdfOptimizationOptions options,
        string qpdfPath,
        CancellationToken cancellationToken = default,
        Action<string>? logAction = null,
        string? displayOutputPath = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("PDF ファイルが見つかりません。", inputPath);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var executablePath = ResolveQpdfPath(qpdfPath);
        var logOutputPath = string.IsNullOrWhiteSpace(displayOutputPath) ? outputPath : displayOutputPath;
        logAction?.Invoke($"PDF変換: {Path.GetFileName(inputPath)} -> {Path.GetFileName(logOutputPath)}");

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in BuildOptimizeArguments(inputPath, outputPath, options))
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var message = GetProcessErrorMessage(stderr, stdout, process.ExitCode);
            throw new InvalidOperationException(message);
        }

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("qpdf の出力ファイルが作成されませんでした。");

        return new FileInfo(outputPath).Length;
    }

    private static IEnumerable<string> BuildOptimizeArguments(string inputPath, string outputPath, PdfOptimizationOptions options)
    {
        yield return inputPath;

        if (options.OptimizeImages)
        {
            yield return "--optimize-images";
            yield return $"--jpeg-quality={Clamp(options.JpegQuality, 1, 100)}";
            yield return $"--oi-min-width={Math.Max(1, options.MinWidth)}";
            yield return $"--oi-min-height={Math.Max(1, options.MinHeight)}";
            yield return $"--oi-min-area={Math.Max(1, options.MinArea)}";
            if (options.KeepInlineImages)
                yield return "--keep-inline-images";
        }

        if (options.CompressStreams)
        {
            yield return "--compress-streams=y";

            var decodeLevel = NormalizeDecodeLevel(options.DecodeLevel);
            if (!string.IsNullOrEmpty(decodeLevel))
                yield return $"--decode-level={decodeLevel}";

            if (options.RecompressFlate)
                yield return "--recompress-flate";

            yield return $"--compression-level={Clamp(options.CompressionLevel, 1, 9)}";
        }
        else
        {
            yield return "--compress-streams=n";
        }

        if (options.StructureCleanup)
        {
            if (options.ExternalizeInlineImages)
            {
                yield return "--externalize-inline-images";
                yield return $"--ii-min-bytes={Math.Max(0, options.InlineImageMinBytes)}";
            }

            yield return $"--object-streams={NormalizeObjectStreamMode(options.ObjectStreamMode)}";

            var removeUnreferencedResources = NormalizeRemoveUnreferencedResources(options.RemoveUnreferencedResources);
            if (removeUnreferencedResources != "auto")
                yield return $"--remove-unreferenced-resources={removeUnreferencedResources}";

            if (options.PreserveUnreferencedObjects)
                yield return "--preserve-unreferenced";

            if (options.NormalizeContent)
                yield return "--normalize-content=y";

            if (options.CoalesceContents)
                yield return "--coalesce-contents";

            if (options.NewlineBeforeEndStream)
                yield return "--newline-before-endstream";
        }

        if (options.DistributionCompatibility)
        {
            var minVersion = NormalizePdfVersion(options.MinVersion);
            if (!string.IsNullOrEmpty(minVersion))
                yield return $"--min-version={minVersion}";

            var forceVersion = NormalizePdfVersion(options.ForceVersion);
            if (!string.IsNullOrEmpty(forceVersion))
                yield return $"--force-version={forceVersion}";

            if (options.Linearize)
                yield return "--linearize";
        }

        if (options.RestrictionRemoval)
        {
            if (options.Decrypt)
                yield return "--decrypt";

            if (options.RemoveRestrictions)
                yield return "--remove-restrictions";
        }

        yield return outputPath;
    }

    private static string ResolveQpdfPath(string qpdfPath)
    {
        foreach (var candidate in EnumerateQpdfCandidates(qpdfPath))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        if (!string.IsNullOrWhiteSpace(qpdfPath) && Path.IsPathRooted(qpdfPath))
            throw new FileNotFoundException("qpdf.exe が見つかりません。設定で qpdf.exe のパスを確認してください。", qpdfPath);

        return "qpdf.exe";
    }

    private static IEnumerable<string> EnumerateQpdfCandidates(string qpdfPath)
    {
        if (!string.IsNullOrWhiteSpace(qpdfPath))
        {
            yield return qpdfPath;
            if (!Path.IsPathRooted(qpdfPath))
            {
                yield return Path.Combine(AppContext.BaseDirectory, qpdfPath);
                yield return Path.Combine(Environment.CurrentDirectory, qpdfPath);
            }
        }

        yield return Path.Combine(AppContext.BaseDirectory, "tools", "qpdf.exe");

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            if (!string.IsNullOrWhiteSpace(dir))
                yield return Path.Combine(dir, "qpdf.exe");
        }

        yield return @"C:\Program Files\qpdf\bin\qpdf.exe";
    }

    private static string GetProcessErrorMessage(string stderr, string stdout, int exitCode)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(line)
            ? $"qpdf がエラー終了しました。終了コード: {exitCode}"
            : line;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static int Clamp(int value, int min, int max)
        => Math.Min(Math.Max(value, min), max);

    private static string NormalizeDecodeLevel(string? value)
        => value switch
        {
            "none" or "generalized" or "specialized" or "all" => value,
            _ => "generalized"
        };

    private static string NormalizeObjectStreamMode(string? value)
        => value switch
        {
            "disable" or "generate" => value,
            _ => "preserve"
        };

    private static string NormalizeRemoveUnreferencedResources(string? value)
        => value switch
        {
            "yes" or "no" => value,
            _ => "auto"
        };

    private static string NormalizePdfVersion(string? value)
        => value switch
        {
            "1.3" or "1.4" or "1.5" or "1.6" or "1.7" or "2.0" => value,
            _ => ""
        };
}
