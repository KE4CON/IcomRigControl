using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

/// Dedicated on/off label for the HRD Logbook bridge toggle. Each ToggleButton
/// gets its own converter (CLAUDE.md rule) so its caption actually describes
/// what it controls, rather than a reused, misleading "Start/Stop Logging".
public static class HrdBridgeButtonTextConverter
{
    public static readonly HrdBridgeButtonTextConverterInstance Instance = new();
}

public class HrdBridgeButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isOn = value is bool b && b;
        return isOn ? "HRD Bridge: ON" : "HRD Bridge: OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
