using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

/// Dedicated on/off label for the remote-audio Transmit toggle (its own
/// converter, per CLAUDE.md's ToggleButton rule).
public static class TransmitButtonTextConverter
{
    public static readonly TransmitButtonTextConverterInstance Instance = new();
}

public class TransmitButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool transmitting = value is bool b && b;
        return transmitting ? "TRANSMITTING — release to receive" : "Transmit (push to talk)";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
