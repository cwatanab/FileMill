using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileMill.Services;

public static class ThemeHelper
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void ApplyWindowTheme(Window window, bool isDark)
    {
        if (window == null) return;

        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            // If the handle is not yet created, hook SourceInitialized
            window.SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                SetDarkTitleBar(h, isDark);
            };
        }
        else
        {
            SetDarkTitleBar(hwnd, isDark);
        }
    }

    private static void SetDarkTitleBar(IntPtr hwnd, bool isDark)
    {
        try
        {
            int dark = isDark ? 1 : 0;
            // Try Windows 11 / Windows 10 (20H1+) attribute
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
            {
                // Fallback to older Windows 10 attribute
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));
            }
        }
        catch
        {
            // Safe fallback
        }
    }
}
