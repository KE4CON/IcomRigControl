using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IcomRigControl.Services;

/// <summary>
/// Free, no-signup callsign lookup against callook.info's public JSON API.
/// US callsigns only (data sourced from FCC records). See CLAUDE.md Phase 8c.
/// </summary>
public class CallookLookupSource : ICallsignLookupSource
{
    private readonly HttpClient _httpClient;

    public string SourceName => "Callook.info";
    public bool RequiresCredentials => false;

    public CallookLookupSource(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CallsignInfo?> LookupAsync(string callsign)
    {
        try
        {
            var url = $"https://callook.info/{Uri.EscapeDataString(callsign)}/json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<CallookResponse>(body);

            if (parsed == null || parsed.Status != "VALID")
            {
                return null;
            }

            return new CallsignInfo(
                Callsign: parsed.Current?.Callsign ?? callsign.ToUpperInvariant(),
                Name: string.IsNullOrWhiteSpace(parsed.Name) ? null : parsed.Name,
                GridSquare: string.IsNullOrWhiteSpace(parsed.Location?.GridSquare) ? null : parsed.Location.GridSquare,
                Address: FormatAddress(parsed.Address),
                LicenseClass: string.IsNullOrWhiteSpace(parsed.Current?.OperClass) ? null : parsed.Current.OperClass
            );
        }
        catch
        {
            // Network failure, malformed JSON, timeout, etc. — never throw,
            // per ICallsignLookupSource contract. A failed lookup should
            // never block the user from logging a QSO manually.
            return null;
        }
    }

    private static string? FormatAddress(CallookAddress? address)
    {
        if (address == null) return null;
        var parts = new[] { address.Line1, address.Line2 }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    // ── JSON deserialization shape, matching callook.info's actual API ──────

    private class CallookResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("current")]
        public CallookCurrent? Current { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public CallookAddress? Address { get; set; }

        [JsonPropertyName("location")]
        public CallookLocation? Location { get; set; }
    }

    private class CallookCurrent
    {
        [JsonPropertyName("callsign")]
        public string? Callsign { get; set; }

        [JsonPropertyName("operClass")]
        public string? OperClass { get; set; }
    }

    private class CallookAddress
    {
        [JsonPropertyName("line1")]
        public string? Line1 { get; set; }

        [JsonPropertyName("line2")]
        public string? Line2 { get; set; }
    }

    private class CallookLocation
    {
        [JsonPropertyName("gridsquare")]
        public string? GridSquare { get; set; }
    }
}