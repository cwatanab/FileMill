using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileMill.Converters;

/// <summary>
/// value (string) が parameter (string) と一致すれば Visible、そうでなければ Collapsed
/// </summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string valStr && parameter is string paramStr)
        {
            return valStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// value (Enum) と parameter (string) が一致すれば true、そうでなければ false。
/// ConvertBack は true になった場合に parameter を Enum 値に復元。
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        var paramString = parameter.ToString();
        if (Enum.IsDefined(value.GetType(), value))
        {
            return value.ToString()?.Equals(paramString, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return DependencyProperty.UnsetValue;
        if (value is true)
        {
            try
            {
                return Enum.Parse(targetType, parameter.ToString() ?? string.Empty);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
        return DependencyProperty.UnsetValue;
    }
}
