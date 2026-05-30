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
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var sectionName = trimmed.Substring(1, trimmed.Length - 2).Trim();
                if (!data.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    data[sectionName] = currentSection;
                }
            }
            else if (currentSection != null)
            {
                int index = trimmed.IndexOf('=');
                if (index > 0)
                {
                    var key = trimmed.Substring(0, index).Trim();
                    var value = trimmed.Substring(index + 1).Trim();
                    currentSection[key] = value;
                }
            }
        }

        return data;
    }

    public static void Save(string filePath, Dictionary<string, Dictionary<string, string>> data)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var sb = new StringBuilder();
            foreach (var sectionPair in data)
            {
                sb.AppendLine($"[{sectionPair.Key}]");
                foreach (var keyPair in sectionPair.Value)
                {
                    sb.AppendLine($"{keyPair.Key}={keyPair.Value}");
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
