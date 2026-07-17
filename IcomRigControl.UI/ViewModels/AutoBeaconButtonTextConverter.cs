using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

public static class AutoBeaconButtonTextConverter
{
    public static readonly AutoBeaconButtonTextConverterInstance Instance = new();
}

public class AutoBeaconButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isOn = value is bool b && b;
        return isOn ? "Auto Beacon: ON" : "Auto Beacon: OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}