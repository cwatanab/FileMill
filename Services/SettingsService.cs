using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileMill.Services;

public class SettingsService
{
    public static Dictionary<string, Dictionary<string, string>> Load(string filePath)
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
            return data;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath, Encoding.UTF8);
        }
        catch
        {
            return data;
        }

        Dictionary<string, string>? currentSection = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (IsIgnoredLine(trimmed))
                continue;

            if (TryGetSectionName(trimmed, out var sectionName))
            {
                if (!data.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    data[sectionName] = currentSection;
                }
            }
            else if (currentSection != null && TryGetKeyValue(trimmed, out var key, out var value))
            {
                currentSection[key] = value;
            }
        }

        return data;
    }

    public static void Save(string filePath, Dictionary<string, Dictionary<string, string>> data)
    {
        try
        {
            EnsureDirectory(filePath);
            File.WriteAllText(filePath, Serialize(data), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private static bool IsIgnoredLine(string line)
        => string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#');

    private static bool TryGetSectionName(string line, out string sectionName)
    {
        if (line.StartsWith('[') && line.EndsWith(']'))
        {
            sectionName = line.Substring(1, line.Length - 2).Trim();
            return true;
        }

        sectionName = "";
        return false;
    }

    private static bool TryGetKeyValue(string line, out string key, out string value)
    {
        var index = line.IndexOf('=');
        if (index > 0)
        {
            key = line.Substring(0, index).Trim();
            value = line.Substring(index + 1).Trim();
            return true;
        }

        key = "";
        value = "";
        return false;
    }

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private static string Serialize(Dictionary<string, Dictionary<string, string>> data)
    {
        var sb = new StringBuilder();
        foreach (var sectionPair in data)
        {
            sb.AppendLine($"[{sectionPair.Key}]");
            foreach (var keyPair in sectionPair.Value)
                sb.AppendLine($"{keyPair.Key}={keyPair.Value}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
