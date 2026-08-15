using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

/// Dedicated on/off label for the "receive logged contacts from N1MM/WSJT-X" toggle.
public static class N1mmReceiveButtonTextConverter
{
    public static readonly N1mmReceiveButtonTextConverterInstance Instance = new();
}

public class N1mmReceiveButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isOn = value is bool b && b;
        return isOn ? "N1MM Receive: ON" : "N1MM Receive: OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
