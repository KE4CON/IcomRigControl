using System.Xml.Linq;

namespace IcomRigControl.Services;

/// A modeled band opening at a time of day (e.g. "20m-17m", "day", "Good").
public record BandCondition(string Band, string TimeOfDay, string Condition);

/// <summary>
/// Solar / propagation numbers and the modeled band conditions, from hamqsl.com's
/// public XML feed (SFI, A/K index, sunspots, and per-band day/night openings).
/// </summary>
public record SolarData(
    string SolarFlux, string AIndex, string KIndex, string Sunspots, string Updated,
    IReadOnlyList<BandCondition> Bands);

/// <summary>
/// Fetches live propagation data so the operator can see "which band is open, and to
/// where, right now" — an operating assistant that pairs with the DX cluster. The XML
/// parsing is separated so it's unit-testable without a network. See CLAUDE.md
/// propagation.
/// </summary>
public sealed class SolarDataService
{
    private readonly HttpClient _http;
    public SolarDataService(HttpClient http) => _http = http;

    public async Task<SolarData?> FetchAsync(string url = "https://www.hamqsl.com/solarxml.php")
    {
        try
        {
            string xml = await _http.GetStringAsync(url);
            return Parse(xml);
        }
        catch
        {
            return null; // offline / blocked / bad feed
        }
    }

    /// Parses the hamqsl solar XML. Missing elements come back as empty strings, so a
    /// partial feed never throws.
    public static SolarData Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var d = doc.Descendants("solardata").FirstOrDefault() ?? doc.Root;

        string Val(string name) => d?.Element(name)?.Value.Trim() ?? "";

        var bands = new List<BandCondition>();
        foreach (var b in doc.Descendants("band"))
        {
            string band = b.Attribute("name")?.Value ?? "";
            string time = b.Attribute("time")?.Value ?? "";
            string cond = b.Value.Trim();
            if (band.Length > 0) bands.Add(new BandCondition(band, time, cond));
        }

        return new SolarData(
            SolarFlux: Val("solarflux"),
            AIndex: Val("aindex"),
            KIndex: Val("kindex"),
            Sunspots: Val("sunspots"),
            Updated: Val("updated"),
            Bands: bands);
    }
}
