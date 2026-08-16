using System.Linq;

namespace IcomRigControl.Services;

/// <summary>
/// Sends a push notification to the operator's phone via ntfy.sh — a free, open,
/// no-signup push service. The operator installs the ntfy app, subscribes to a topic
/// name, and this posts alerts (a needed DX spotted, an SWR trip, an APRS message) to
/// that topic. Defensive: any network failure returns false, never throws. See
/// CLAUDE.md push alerts.
/// </summary>
public sealed class PushNotifier
{
    private readonly HttpClient _http;

    public PushNotifier(HttpClient http) => _http = http;

    /// A topic is a URL path segment; keep only safe characters.
    public static string SanitizeTopic(string? topic) =>
        new string((topic ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    public async Task<bool> SendAsync(string topic, string title, string message)
    {
        string t = SanitizeTopic(topic);
        if (t.Length == 0) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://ntfy.sh/{t}")
            {
                Content = new StringContent(message)
            };
            req.Headers.TryAddWithoutValidation("Title", title);
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
