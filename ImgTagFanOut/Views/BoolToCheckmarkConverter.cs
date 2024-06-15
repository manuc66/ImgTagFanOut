using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ImgTagFanOut.Views;

public class BoolToCheckmarkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "✔️" : string.Empty;
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}