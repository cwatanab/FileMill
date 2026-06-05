using System;
using System.Collections.Generic;
using System.Globalization;

namespace FileMill.Helpers;

public static class SettingsValueReader
{
    public static string ReadString(Dictionary<string, string> section, string key, string currentValue, bool requireNonWhiteSpace = false)
    {
        if (!section.TryGetValue(key, out var value))
            return currentValue;

        return requireNonWhiteSpace && string.IsNullOrWhiteSpace(value) ? currentValue : value;
    }

    public static bool ReadBool(Dictionary<string, string> section, string key, bool currentValue)
        => section.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : currentValue;

    public static int ReadInt(Dictionary<string, string> section, string key, int currentValue)
        => section.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : currentValue;

    public static double ReadDouble(Dictionary<string, string> section, string key, double currentValue)
        => section.TryGetValue(key, out var value)
           && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : currentValue;

    public static TEnum ReadEnum<TEnum>(Dictionary<string, string> section, string key, TEnum currentValue)
        where TEnum : struct, Enum
        => section.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, out var parsed)
            ? parsed
            : currentValue;

    public static bool TryReadBool(Dictionary<string, string> section, string key, out bool value)
    {
        if (section.TryGetValue(key, out var rawValue) && bool.TryParse(rawValue, out value))
            return true;

        value = default;
        return false;
    }
}
