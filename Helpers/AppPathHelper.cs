using System;
using System.IO;

namespace FileMill.Helpers;

public static class AppPathHelper
{
    public static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "FileMill", "settings.ini");
    }

    public static string GetPresetDirectoryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "FileMill", "presets");
    }

    public static string GetPresetFilePath(string presetName)
    {
        return Path.Combine(GetPresetDirectoryPath(), SanitizePresetName(presetName) + ".ini");
    }

    public static string SanitizePresetName(string name)
    {
        var sanitized = name.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(invalidChar, '_');
        return sanitized;
    }
}
