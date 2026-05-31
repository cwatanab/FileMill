using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using FileMill.ViewModels;
using FileMill.Models;

namespace FileMill;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private AppTheme _currentTheme = AppTheme.Auto;

    /// <summary>
    /// テーマを適用する。MainWindow の DataContext から ThemeMode を読み取って呼び出す。
    /// </summary>
    public static void ApplyTheme(AppTheme mode)
    {
        var app = (App)Current;
        app._currentTheme = mode;

        var resolvedMode = mode;
        if (mode == AppTheme.Auto)
        {
            resolvedMode = IsSystemDarkMode() ? AppTheme.Dark : AppTheme.Light;
        }

        var themeName = resolvedMode == AppTheme.Dark
            ? "DarkTheme.xaml"
            : "LightTheme.xaml";

        var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{themeName}") };

        // 既存のテーマ辞書を置き換え（ThemeColors.xaml はそのまま）
        var merged = app.Resources.MergedDictionaries;
        // 2番目以降にテーマ辞書があれば削除（1番目は ThemeColors.xaml）
        while (merged.Count > 1)
            merged.RemoveAt(1);
        merged.Add(dict);

        // すべてのウィンドウのタイトルバーにテーマを適用
        bool isDark = resolvedMode == AppTheme.Dark;
        foreach (Window window in app.Windows)
        {
            Services.ThemeHelper.ApplyWindowTheme(window, isDark);
        }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            var value = Registry.GetValue(key, "AppsUseLightTheme", 1);
            return value is 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsDarkThemeActive()
    {
        var app = (App)Current;
        var mode = app._currentTheme;
        if (mode == AppTheme.Auto)
        {
            return IsSystemDarkMode();
        }
        return mode == AppTheme.Dark;
    }

    public static AppTheme CurrentTheme => ((App)Current)._currentTheme;

    /// <summary>
    /// settings.ini から Language キーを読み取り、UI カルチャを設定する。
    /// </summary>
    private static void ApplySavedLanguage()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(appData, "FileMill", "settings.ini");
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path);
            bool inGeneral = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "[General]") { inGeneral = true; continue; }
                if (trimmed.StartsWith('[')) { inGeneral = false; continue; }
                if (inGeneral && trimmed.StartsWith("Language=", StringComparison.OrdinalIgnoreCase))
                {
                    var lang = trimmed.Substring("Language=".Length).Trim();
                    var culture = lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "ja-JP";
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                    break;
                }
            }
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // OSのテーマ設定変更を監視
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        // 保存された言語設定を読み取ってカルチャを適用
        ApplySavedLanguage();

        if (e.Args.Contains("--test-settings"))
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var settingsPath = Path.Combine(appData, "FileMill", "settings_test.ini");

                RunSettingsTest(settingsPath);
                RunModalStateTest(settingsPath);
                RunListManagementTest(settingsPath);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex}");
                Environment.Exit(1);
            }
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        base.OnExit(e);
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle)
        {
            if (_currentTheme == AppTheme.Auto)
            {
                Dispatcher.BeginInvoke(new Action(() => ApplyTheme(AppTheme.Auto)));
            }
        }
    }

    private void RunSettingsTest(string settingsPath)
    {
        // 1. Delete existing settings to ensure clean test
        if (File.Exists(settingsPath))
        {
            File.Delete(settingsPath);
        }

        // 2. Verify defaults
        var vm1 = new MainViewModel(settingsPath);
        var failures = new System.Text.StringBuilder();
        if (vm1.PlaySoundOnComplete != true) failures.AppendLine($"PlaySoundOnComplete={vm1.PlaySoundOnComplete}");
        if (vm1.ConfirmOnClear != true) failures.AppendLine($"ConfirmOnClear={vm1.ConfirmOnClear}");
        if (vm1.FileNameRule != "_converted") failures.AppendLine($"FileNameRule='{vm1.FileNameRule}'");
        if (vm1.UseOxipng != false) failures.AppendLine($"UseOxipng={vm1.UseOxipng}");
        if (vm1.OxipngLevel != 2) failures.AppendLine($"OxipngLevel={vm1.OxipngLevel}");
        if (vm1.OxipngPath != "tools/oxipng.exe") failures.AppendLine($"OxipngPath='{vm1.OxipngPath}'");
        if (vm1.UseJpegli != false) failures.AppendLine($"UseJpegli={vm1.UseJpegli}");
        if (vm1.CjpegliPath != "tools/cjpegli.exe") failures.AppendLine($"CjpegliPath='{vm1.CjpegliPath}'");
        if (vm1.EnableOfficeOptimize != true) failures.AppendLine($"EnableOfficeOptimize={vm1.EnableOfficeOptimize}");
        if (vm1.EnableImageOptimize != true) failures.AppendLine($"EnableImageOptimize={vm1.EnableImageOptimize}");
        if (vm1.StripOfficeMetadata != true) failures.AppendLine($"StripOfficeMetadata={vm1.StripOfficeMetadata}");
        if (vm1.CleanUnusedObjects != true) failures.AppendLine($"CleanUnusedObjects={vm1.CleanUnusedObjects}");
        if (vm1.CompressImages != true) failures.AppendLine($"CompressImages={vm1.CompressImages}");
        if (vm1.ConvertToWebP != false) failures.AppendLine($"ConvertToWebP={vm1.ConvertToWebP}");
        if (vm1.CompressEmbeddedImages != true) failures.AppendLine($"CompressEmbeddedImages={vm1.CompressEmbeddedImages}");
        if (vm1.CompressMedia != true) failures.AppendLine($"CompressMedia={vm1.CompressMedia}");
        if (vm1.MediaVideoCrf != 23) failures.AppendLine($"MediaVideoCrf={vm1.MediaVideoCrf}");
        if (vm1.MediaVideoCodec != "libx264") failures.AppendLine($"MediaVideoCodec='{vm1.MediaVideoCodec}'");
        if (vm1.MediaAudioCodec != "aac") failures.AppendLine($"MediaAudioCodec='{vm1.MediaAudioCodec}'");
        if (vm1.FfmpegPath != "tools/ffmpeg.exe") failures.AppendLine($"FfmpegPath='{vm1.FfmpegPath}'");
        if (failures.Length > 0)
        {
            throw new Exception("Initial settings are not default values.\n" + failures);
        }

        // 3. Edit settings
        vm1.PlaySoundOnComplete = false;
        vm1.ConfirmOnClear = false;
        vm1.FileNameRule = "_test";
        vm1.WebPQuality = 92;
        vm1.SelectedTabIndex = 1;
        vm1.WindowWidth = 1100;
        vm1.WindowHeight = 780;
        vm1.WindowLeft = 100;
        vm1.WindowTop = 150;
        vm1.WindowState = WindowState.Maximized;
        vm1.UseOxipng = true;
        vm1.OxipngPath = @"C:\tools\oxipng.exe";
        vm1.OxipngLevel = 4;
        vm1.CjpegliPath = @"C:\tools\cjpegli.exe";
        vm1.UseJpegli = true;
        vm1.EnableOfficeOptimize = false;
        vm1.EnableImageOptimize = false;
        vm1.StripOfficeMetadata = false;
        vm1.CleanUnusedObjects = false;
        vm1.CompressImages = false;
        vm1.ConvertToWebP = true;
        vm1.CompressEmbeddedImages = false;
        vm1.CompressMedia = false;
        vm1.MediaVideoCrf = 28;
        vm1.MediaVideoCodec = "libx265";
        vm1.MediaAudioCodec = "aac";
        vm1.FfmpegPath = @"C:\tools\ffmpeg.exe";

        // Edit steps
        var resizeStep = vm1.Steps.FirstOrDefault(s => s.Type == PipelineStepType.Resize);
        if (resizeStep != null)
        {
            resizeStep.Enabled = true;
            resizeStep.TargetWidth = 800;
            resizeStep.TargetHeight = 600;
            resizeStep.FitMode = FitMode.Cover;
        }

        var rotateStep = vm1.Steps.FirstOrDefault(s => s.Type == PipelineStepType.Rotate);
        if (rotateStep != null)
        {
            rotateStep.Enabled = true;
            rotateStep.RotateTarget = RotateTarget.Landscape;
            rotateStep.RotationAngleIndex = 2; // 270 degrees
        }

        var grayscaleStep = vm1.Steps.FirstOrDefault(s => s.Type == PipelineStepType.Grayscale);
        if (grayscaleStep != null)
        {
            grayscaleStep.Enabled = false; // 無効化（削除ではなく）
        }

        // 4. Save settings
        vm1.SaveSettings(settingsPath);

        // 5. Load settings in another VM instance
        var vm2 = new MainViewModel(settingsPath);

        // 6. Assert restored options
        if (vm2.PlaySoundOnComplete != false) throw new Exception("PlaySoundOnComplete was not loaded correctly.");
        if (vm2.ConfirmOnClear != false) throw new Exception("ConfirmOnClear was not loaded correctly.");
        if (vm2.FileNameRule != "_test") throw new Exception("FileNameRule was not loaded correctly.");
        if (vm2.WebPQuality != 92) throw new Exception("WebPQuality was not loaded correctly.");
        if (vm2.SelectedTabIndex != 1) throw new Exception("SelectedTabIndex was not loaded correctly.");
        if (Math.Abs(vm2.WindowWidth - 1100) > 0.001) throw new Exception("WindowWidth was not loaded correctly.");
        if (Math.Abs(vm2.WindowHeight - 780) > 0.001) throw new Exception("WindowHeight was not loaded correctly.");
        if (Math.Abs(vm2.WindowLeft - 100) > 0.001) throw new Exception("WindowLeft was not loaded correctly.");
        if (Math.Abs(vm2.WindowTop - 150) > 0.001) throw new Exception("WindowTop was not loaded correctly.");
        if (vm2.OxipngPath != @"C:\tools\oxipng.exe") throw new Exception("OxipngPath was not loaded correctly.");
        if (vm2.WindowState != WindowState.Maximized) throw new Exception("WindowState was not loaded correctly.");
        if (vm2.CjpegliPath != @"C:\tools\cjpegli.exe") throw new Exception("CjpegliPath was not loaded correctly.");
        if (vm2.UseOxipng != true) throw new Exception("UseOxipng was not loaded correctly.");
        if (vm2.OxipngLevel != 4) throw new Exception("OxipngLevel was not loaded correctly.");
        if (vm2.UseJpegli != true) throw new Exception("UseJpegli was not loaded correctly.");
        if (vm2.EnableOfficeOptimize != false) throw new Exception("EnableOfficeOptimize was not loaded correctly.");
        if (vm2.EnableImageOptimize != false) throw new Exception("EnableImageOptimize was not loaded correctly.");
        if (vm2.StripOfficeMetadata != false) throw new Exception("StripOfficeMetadata was not loaded correctly.");
        if (vm2.CleanUnusedObjects != false) throw new Exception("CleanUnusedObjects was not loaded correctly.");
        if (vm2.CompressImages != false) throw new Exception("CompressImages was not loaded correctly.");
        if (vm2.ConvertToWebP != true) throw new Exception("ConvertToWebP was not loaded correctly.");
        if (vm2.CompressEmbeddedImages != false) throw new Exception("CompressEmbeddedImages was not loaded correctly.");
        if (vm2.CompressMedia != false) throw new Exception("CompressMedia was not loaded correctly.");
        if (vm2.MediaVideoCrf != 28) throw new Exception("MediaVideoCrf was not loaded correctly.");
        if (vm2.MediaVideoCodec != "libx265") throw new Exception("MediaVideoCodec was not loaded correctly.");
        if (vm2.MediaAudioCodec != "aac") throw new Exception("MediaAudioCodec was not loaded correctly.");
        if (vm2.FfmpegPath != @"C:\tools\ffmpeg.exe") throw new Exception("FfmpegPath was not loaded correctly.");

        var vm2Resize = vm2.Steps.FirstOrDefault(s => s.Type == PipelineStepType.Resize);
        if (vm2Resize == null) throw new Exception("Resize step was lost.");
        if (vm2Resize.Enabled != true) throw new Exception("Resize step Enabled was not restored.");
        if (vm2Resize.TargetWidth != 800) throw new Exception("Resize step TargetWidth was not restored.");
        if (vm2Resize.TargetHeight != 600) throw new Exception("Resize step TargetHeight was not restored.");
        if (vm2Resize.FitMode != FitMode.Cover) throw new Exception("Resize step FitMode was not restored.");

        var vm2Rotate = vm2.Steps.FirstOrDefault(s => s.Type == PipelineStepType.Rotate);
        if (vm2Rotate == null) throw new Exception("Rotate step was lost.");
        if (vm2Rotate.Enabled != true) throw new Exception("Rotate step Enabled was not restored.");
        if (vm2Rotate.RotateTarget != RotateTarget.Landscape) throw new Exception("Rotate step RotateTarget was not restored.");
        if (vm2Rotate.RotationAngleIndex != 2) throw new Exception("Rotate step RotationAngleIndex was not restored.");

        var vm2Grayscale = vm2.Steps.FirstOrDefault(s => s.Type == PipelineStepType.Grayscale);
        if (vm2Grayscale == null) throw new Exception("Grayscale step was removed from defaults.");
        if (vm2Grayscale.Enabled != false) throw new Exception("Grayscale step should be disabled after loading.");

        // Clean up test settings file
        if (File.Exists(settingsPath))
        {
            File.Delete(settingsPath);
        }

        Console.WriteLine("=== Settings persistence test passed successfully ===");
    }

    private void RunModalStateTest(string settingsPath)
    {
        var vm = new MainViewModel(settingsPath);

        // 1. ActiveModalType は初期状態で空
        if (vm.ActiveModalType != "") throw new Exception("ActiveModalType is not empty initially.");

        // 2. RequestOpenSettings に捕捉用コールバックを登録
        string? lastOpened = null;
        vm.RequestOpenSettings = type => { lastOpened = type; };

        // 3. OpenModalCommand でモーダルを開く
        vm.OpenModalCommand.Execute("OfficeOptimize");
        if (lastOpened != "OfficeOptimize") throw new Exception("RequestOpenSettings was not called with OfficeOptimize.");
        if (vm.ActiveModalType != "OfficeOptimize") throw new Exception("ActiveModalType was not set correctly.");
        if (vm.ModalTitle != "Office最適化設定") throw new Exception("ModalTitle was not set correctly for OfficeOptimize.");

        // 4. 別の設定を開く
        vm.OpenModalCommand.Execute("ImageOptimize");
        if (lastOpened != "ImageOptimize") throw new Exception("RequestOpenSettings was not called with ImageOptimize.");
        if (vm.ActiveModalType != "ImageOptimize") throw new Exception("ActiveModalType was not updated.");
        if (vm.ModalTitle != "画像最適化設定") throw new Exception("ModalTitle was not updated for ImageOptimize.");

        Console.WriteLine("=== Modal state management test passed successfully ===");
    }

    private void RunListManagementTest(string settingsPath)
    {
        var vm = new MainViewModel(settingsPath);
        vm.ConfirmOnClear = false;

        // 1. Image Conversion List Test
        if (!vm.IsFileListEmpty) throw new Exception("IsFileListEmpty is not true initially.");
        
        var mockFile1 = new FileMill.Models.ImageFile { FilePath = "test.png", FileSize = 12345 };
        vm.Files.Add(mockFile1);
        if (vm.IsFileListEmpty) throw new Exception("IsFileListEmpty was not set to false after adding a file.");

        vm.ClearFilesCommand.Execute(null);
        if (!vm.IsFileListEmpty || vm.Files.Count > 0) throw new Exception("ClearFilesCommand failed to clear the file list.");

        // 2. File Optimization List Test
        if (!vm.IsOptimizeFileListEmpty) throw new Exception("IsOptimizeFileListEmpty is not true initially.");

        var mockFile2 = new FileMill.Models.OptimizeFile { FilePath = "document.docx", Status = "待機中" };
        vm.OptimizeFiles.Add(mockFile2);
        if (vm.IsOptimizeFileListEmpty) throw new Exception("IsOptimizeFileListEmpty was not set to false after adding a file.");

        vm.ClearOptimizeFilesCommand.Execute(null);
        if (!vm.IsOptimizeFileListEmpty || vm.OptimizeFiles.Count > 0) throw new Exception("ClearOptimizeFilesCommand failed to clear the optimization file list.");

        Console.WriteLine("=== List management test passed successfully ===");
    }
}
