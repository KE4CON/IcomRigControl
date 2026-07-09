using Avalonia.Media;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Converts PttActive (bool) to a color: red when transmitting, dark gray when receiving.
/// </summary>
public static class BoolToColorConverter
{
    public static readonly BoolToColorConverterInstance Instance = new();
}

public class BoolToColorConverterInstance : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool active = value is bool b && b;
        return active ? Colors.Red : Colors.DimGray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}