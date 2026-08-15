namespace IcomRigControl.CivEngine;

/// <summary>
/// Validates whether a string is a plausible amateur-radio callsign that is
/// safe to transmit as a station identity. This is a guard against putting a
/// blank, placeholder, or obviously-invalid identifier on the air — the FCC
/// (and every other regulator) requires a station to identify with its real
/// assigned callsign. It is intentionally a sanity check, not a full ITU
/// prefix validator: it rejects the things that must never be transmitted
/// (empty, "NOCALL", punctuation, no-digit strings) without trying to prove a
/// callsign is genuinely issued.
/// </summary>
public static class CallsignValidator
{
    private static readonly HashSet<string> KnownPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "NOCALL", "N0CALL", "NONE", "TEST", "MYCALL", "CALL", "CALLSIGN", "XXXX", "NULL"
    };

    /// True if <paramref name="callsign"/> looks like a real amateur callsign
    /// that may be transmitted: 3-6 characters (the AX.25 address field limit),
    /// letters and digits only, containing at least one letter AND at least one
    /// digit, and not a known placeholder.
    public static bool IsPlausibleAmateurCallsign(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return false;

        string c = callsign.Trim();
        if (c.Length < 3 || c.Length > 6) return false;

        bool hasLetter = false, hasDigit = false;
        foreach (char ch in c)
        {
            if (char.IsLetter(ch)) hasLetter = true;
            else if (char.IsDigit(ch)) hasDigit = true;
            else return false; // no punctuation or whitespace in a base callsign
        }

        if (!hasLetter || !hasDigit) return false;
        if (KnownPlaceholders.Contains(c)) return false;

        return true;
    }
}
