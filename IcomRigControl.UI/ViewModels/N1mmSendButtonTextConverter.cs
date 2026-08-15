using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

/// Dedicated on/off label for the "send rig state to N1MM/WSJT-X" toggle.
public static class N1mmSendButtonTextConverter
{
    public static readonly N1mmSendButtonTextConverterInstance Instance = new();
}

public class N1mmSendButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isOn = value is bool b && b;
        return isOn ? "N1MM Send: ON" : "N1MM Send: OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
