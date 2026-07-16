using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;

namespace IcomRigControl.UI.ViewModels;

/// <summary>
/// Multi-value converter: given [callsign, confirmedCallsignsCollection],
/// returns "✓" if the callsign is present in the collection, otherwise "".
/// Used by QsoLoggerWindow to show a LoTW-confirmed indicator per row.
/// </summary>
public class LotwConfirmedConverter : IMultiValueConverter
{
    public static readonly LotwConfirmedConverter Instance = new();

    public object? Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return "";
        if (values[0] is not string callsign) return "";
        if (values[1] is not IEnumerable confirmedList) return "";

        bool isConfirmed = confirmedList.Cast<object>()
            .Any(c => c is string s && s.Equals(callsign, StringComparison.OrdinalIgnoreCase));

        return isConfirmed ? "✓" : "";
    }
}