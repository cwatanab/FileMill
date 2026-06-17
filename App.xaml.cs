using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
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

        // Apply Wpf.Ui Application Theme
        var wpfUiTheme = resolvedMode == AppTheme.Dark 
            ? Wpf.Ui.Appearance.ApplicationTheme.Dark 
            : Wpf.Ui.Appearance.ApplicationTheme.Light;
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(wpfUiTheme);

        var themeName = resolvedMode == AppTheme.Dark
            ? "DarkTheme.xaml"
            : "LightTheme.xaml";

        var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{themeName}") };

        // 既存のテーマ辞書を置き換え（MergedDictionaries のインデックス3）
        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 3)
        {
            merged[3] = dict;
        }
        else
        {
            merged.Add(dict);
        }

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

            var data = FileMill.Services.SettingsService.Load(path);
            if (data.TryGetValue("General", out var general) && general.TryGetValue("Language", out var lang))
            {
                var culture = lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "ja-JP";
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplySavedLanguage failed: {ex.Message}");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // OSのテーマ設定変更を監視
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        // 保存された言語設定を読み取ってカルチャを適用
        ApplySavedLanguage();
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

}
