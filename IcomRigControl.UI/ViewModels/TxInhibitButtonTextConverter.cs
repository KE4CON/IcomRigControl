using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

/// Dedicated on/off label for the TX-Inhibit (Receive-Only) toggle (its own
/// converter, per CLAUDE.md's ToggleButton rule).
public static class TxInhibitButtonTextConverter
{
    public static readonly TxInhibitButtonTextConverterInstance Instance = new();
}

public class TxInhibitButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool inhibited = value is bool b && b;
        return inhibited ? "RX ONLY (transmit blocked)" : "TX Inhibit: OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
