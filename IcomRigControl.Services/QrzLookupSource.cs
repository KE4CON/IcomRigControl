using System.Net.Http;
using System.Xml.Linq;

namespace IcomRigControl.Services;

/// <summary>
/// Callsign lookup against QRZ.com's XML API. Requires a paid QRZ XML Data
/// subscription and the user's own QRZ.com username/password. Manages the
/// session key internally: logs in once, caches the key, and reuses it for
/// subsequent lookups (per QRZ's documented "one login per session" pattern).
/// See CLAUDE.md Phase 8c and the credential-storage rule in What NOT to do.
/// </summary>
public class QrzLookupSource : ICallsignLookupSource
{
    private const string BaseUrl = "https://xmldata.qrz.com/xml/";

    private readonly HttpClient _httpClient;
    private readonly string _username;
    private readonly string _password;
    private string? _sessionKey;

    public string SourceName => "QRZ.com";
    public bool RequiresCredentials => true;

    public QrzLookupSource(HttpClient httpClient, string username, string password)
    {
        _httpClient = httpClient;
        _username = username;
        _password = password;
    }

    public async Task<CallsignInfo?> LookupAsync(string callsign)
    {
        try
        {
            if (_sessionKey == null)
            {
                var loggedIn = await LoginAsync();
                if (!loggedIn) return null;
            }

            var url = $"{BaseUrl}?s={_sessionKey}&callsign={Uri.EscapeDataString(callsign)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(body);
            var root = doc.Root;
            if (root == null) return null;

            var ns = root.GetDefaultNamespace();

            var sessionError = root.Element(ns + "Session")?.Element(ns + "Error")?.Value;
            if (!string.IsNullOrEmpty(sessionError))
            {
                // Session may have expired server-side — clear it so the
                // next lookup attempt re-logs in, but this lookup fails.
                if (sessionError.Contains("Session", StringComparison.OrdinalIgnoreCase))
                {
                    _sessionKey = null;
                }
                return null;
            }

            var callsignNode = root.Element(ns + "Callsign");
            if (callsignNode == null) return null;

            string call = callsignNode.Element(ns + "call")?.Value ?? callsign.ToUpperInvariant();
            string? fname = callsignNode.Element(ns + "fname")?.Value;
            string? lname = callsignNode.Element(ns + "name")?.Value;
            string? name = string.IsNullOrWhiteSpace($"{fname} {lname}".Trim())
                ? null
                : $"{fname} {lname}".Trim();
            string? grid = callsignNode.Element(ns + "grid")?.Value;
            string? addr1 = callsignNode.Element(ns + "addr1")?.Value;
            string? addr2 = callsignNode.Element(ns + "addr2")?.Value;
            string? licenseClass = callsignNode.Element(ns + "class")?.Value;

            var addressParts = new[] { addr1, addr2 }.Where(p => !string.IsNullOrWhiteSpace(p));
            string? address = string.Join(", ", addressParts);
            if (string.IsNullOrWhiteSpace(address)) address = null;

            return new CallsignInfo(
                Callsign: call,
                Name: string.IsNullOrWhiteSpace(name) ? null : name,
                GridSquare: string.IsNullOrWhiteSpace(grid) ? null : grid,
                Address: address,
                LicenseClass: string.IsNullOrWhiteSpace(licenseClass) ? null : licenseClass
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
            var url = $"{BaseUrl}?username={Uri.EscapeDataString(_username)}&password={Uri.EscapeDataString(_password)}&agent=IcomRigControl";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(body);
            var root = doc.Root;
            if (root == null) return false;

            var ns = root.GetDefaultNamespace();
            var key = root.Element(ns + "Session")?.Element(ns + "Key")?.Value;

            if (string.IsNullOrEmpty(key)) return false;

            _sessionKey = key;
            return true;
        }
        catch
        {
            return false;
        }
    }
}