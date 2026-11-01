using System;
using System.Globalization;
using System.Windows.Data;

namespace NEXUS.Desktop.Converters;

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return "False";
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase) ? "True" : "False";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
