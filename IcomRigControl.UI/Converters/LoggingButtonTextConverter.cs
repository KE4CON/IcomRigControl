using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

public static class LoggingButtonTextConverter
{
    public static readonly LoggingButtonTextConverterInstance Instance = new();
}

public class LoggingButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool logging = value is bool b && b;
        return logging ? "Stop Logging" : "Start Logging";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}