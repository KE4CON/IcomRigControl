using System.Globalization;
using System.Xml.Linq;

namespace IcomRigControl.Services;

/// <summary>
/// Parses the XML "contactinfo" packets broadcast by N1MM Logger+ (and, via
/// the same shared protocol family, WSJT-X and other compatible programs)
/// whenever a QSO is logged there. See CLAUDE.md Phase 8f for background.
/// </summary>
public static class ContactPacketParser
{
    /// Parses a Contact XML packet into a QsoRecord. Returns null if the XML
    /// is malformed, isn't a contactinfo packet, or is missing the callsign.
    public static QsoRecord? Parse(string xml)
    {
        XElement root;
        try
        {
            root = XElement.Parse(xml);
        }
        catch
        {
            return null;
        }

        if (root.Name.LocalName != "contactinfo")
        {
            return null;
        }

        string? call = root.Element("call")?.Value;
        if (string.IsNullOrWhiteSpace(call))
        {
            return null;
        }

        double frequencyMHz = 0;
        var rxFreqStr = root.Element("rxfreq")?.Value;
        if (!string.IsNullOrWhiteSpace(rxFreqStr) &&
            long.TryParse(rxFreqStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long rxFreqHz))
        {
            frequencyMHz = rxFreqHz / 1_000_000.0;
        }

        string mode = root.Element("mode")?.Value ?? "USB";
        string band = root.Element("band")?.Value ?? "";
        string rstSent = root.Element("snt")?.Value ?? "";
        string rstReceived = root.Element("rcv")?.Value ?? "";
        string? exchangeSent = root.Element("exchange1")?.Value;
        string? exchangeReceived = root.Element("sect")?.Value;

        DateTime timestampUtc = DateTime.UtcNow;
        var timestampStr = root.Element("timestamp")?.Value;
        if (!string.IsNullOrWhiteSpace(timestampStr) &&
            DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedTimestamp))
        {
            timestampUtc = parsedTimestamp;
        }

        return new QsoRecord(
            Callsign: call.ToUpperInvariant(),
            FrequencyMHz: frequencyMHz,
            Band: string.IsNullOrWhiteSpace(band) ? $"{(int)frequencyMHz}M" : $"{band}M",
            Mode: mode,
            DateTimeUtc: timestampUtc,
            RstSent: rstSent,
            RstReceived: rstReceived,
            ContestExchangeSent: string.IsNullOrWhiteSpace(exchangeSent) ? null : exchangeSent,
            ContestExchangeReceived: string.IsNullOrWhiteSpace(exchangeReceived) ? null : exchangeReceived
        );
    }
}