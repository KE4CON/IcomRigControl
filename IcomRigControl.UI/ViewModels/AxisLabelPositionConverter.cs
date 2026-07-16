using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Converts a 0.0-1.0 PixelFraction into a Canvas.Left value, given the
/// available width passed as ConverterParameter (a rough approximation
/// of the waterfall's rendered width, since Canvas doesn't expose live
/// binding to a sibling control's actual width easily).
/// </summary>
public static class AxisLabelPositionConverter
{
    public static readonly AxisLabelPositionConverterInstance Instance = new();
}

public class AxisLabelPositionConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double fraction) return 0.0;
        double width = parameter is string s && double.TryParse(s, out double w) ? w : 440;
        return fraction * width;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}