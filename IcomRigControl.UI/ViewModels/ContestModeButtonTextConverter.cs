using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace IcomRigControl.UI.ViewModels;

public static class ContestModeButtonTextConverter
{
    public static readonly ContestModeButtonTextConverterInstance Instance = new();
}

public class ContestModeButtonTextConverterInstance : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isOn = value is bool b && b;
        return isOn ? "Contest Mode: ON" : "Contest Mode: OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}