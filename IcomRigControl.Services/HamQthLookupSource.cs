using System.Net.Http;
using System.Xml.Linq;

namespace IcomRigControl.Services;

/// <summary>
/// Callsign lookup against HamQTH.com's XML API. Free service, but requires
/// a free registered HamQTH account (username/password). Manages the
/// session ID internally: logs in once, caches it, and reuses it for
/// subsequent lookups (session IDs are valid for one hour per HamQTH docs).
/// See CLAUDE.md Phase 8c.
/// </summary>
public class HamQthLookupSource : ICallsignLookupSource
{
    private const string BaseUrl = "https://www.hamqth.com/xml.php";

    private readonly HttpClient _httpClient;
    private readonly string _username;
    private readonly string _password;
    private string? _sessionId;

    public string SourceName => "HamQTH.com";
    public bool RequiresCredentials => true;

    public HamQthLookupSource(HttpClient httpClient, string username, string password)
    {
        _httpClient = httpClient;
        _username = username;
        _password = password;
    }

    public async Task<CallsignInfo?> LookupAsync(string callsign)
    {
        try
        {
            if (_sessionId == null)
            {
                var loggedIn = await LoginAsync();
                if (!loggedIn) return null;
            }

            var url = $"{BaseUrl}?id={_sessionId}&callsign={Uri.EscapeDataString(callsign)}&prg=IcomRigControl";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(body);
            var root = doc.Root;
            if (root == null) return null;

            var ns = root.GetDefaultNamespace();

            var sessionError = root.Element(ns + "session")?.Element(ns + "error")?.Value;
            if (!string.IsNullOrEmpty(sessionError))
            {
                if (sessionError.Contains("Session", StringComparison.OrdinalIgnoreCase))
                {
                    _sessionId = null;
                }
                return null;
            }

            var searchNode = root.Element(ns + "search");
            if (searchNode == null) return null;

            string call = searchNode.Element(ns + "callsign")?.Value ?? callsign.ToUpperInvariant();
            string? name = searchNode.Element(ns + "adr_name")?.Value;
            string? grid = searchNode.Element(ns + "grid")?.Value;
            string? street = searchNode.Element(ns + "adr_street1")?.Value;
            string? city = searchNode.Element(ns + "adr_city")?.Value;

            var addressParts = new[] { street, city }.Where(p => !string.IsNullOrWhiteSpace(p));
            string? address = string.Join(", ", addressParts);
            if (string.IsNullOrWhiteSpace(address)) address = null;

            return new CallsignInfo(
                Callsign: call,
                Name: string.IsNullOrWhiteSpace(name) ? null : name,
                GridSquare: string.IsNullOrWhiteSpace(grid) ? null : grid,
                Address: address,
                LicenseClass: null // HamQTH does not expose a license class field
            );
        }
        catch
        {
            // Never throw, per ICallsignLookupSource contract.
            return null;
        }
    }

    private async Task<bool> LoginAsync()
    {
        try
        {
            var url = $"{BaseUrl}?u={Uri.EscapeDataString(_username)}&p={Uri.EscapeDataString(_password)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(body);
            var root = doc.Root;
            if (root == null) return false;

            var ns = root.GetDefaultNamespace();
            var sessionId = root.Element(ns + "session")?.Element(ns + "session_id")?.Value;

            if (string.IsNullOrEmpty(sessionId)) return false;

            _sessionId = sessionId;
            return true;
        }
        catch
        {
            return false;
        }
    }
}