using System.Security.Cryptography;
using System.Text;

namespace IcomRigControl.RigModel;

/// <summary>
/// The wire protocol for IcomRigControl's TCP remote-control feature (Phase 9).
/// A client must send a correctly-tokened AUTH request before the server will
/// relay any CI-V frames. Designed for use over a 44Net/AMPRNet link or any
/// TCP-reachable network — auth is required precisely because such links can
/// be reachable by more than just the operator's own LAN.
/// </summary>
public static class CivNetworkProtocol
{
    private const string AuthPrefix = "AUTH:";
    public static readonly byte[] AuthSuccessResponse = Encoding.UTF8.GetBytes("AUTH_OK");
    public static readonly byte[] AuthFailureResponse = Encoding.UTF8.GetBytes("AUTH_FAIL");

    public static byte[] BuildAuthRequest(string token)
    {
        return Encoding.UTF8.GetBytes(AuthPrefix + token);
    }

    public static bool TryParseAuthRequest(byte[] data, out string? token)
    {
        token = null;
        string text;
        try
        {
            text = Encoding.UTF8.GetString(data);
        }
        catch
        {
            return false;
        }

        if (!text.StartsWith(AuthPrefix))
        {
            return false;
        }

        token = text[AuthPrefix.Length..];
        return true;
    }

    public static bool IsAuthSuccess(byte[] response)
    {
        return response.AsSpan().SequenceEqual(AuthSuccessResponse);
    }

    /// Constant-time comparison to avoid leaking token length/content via
    /// timing differences — a reasonable precaution given this authenticates
    /// remote control of a live transmitter over a network link.
    public static bool ValidateToken(string expectedToken, string providedToken)
    {
        if (string.IsNullOrEmpty(expectedToken))
        {
            // An unset/blank expected token must never validate — prevents
            // accidentally running an open, unauthenticated server.
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        byte[] providedBytes = Encoding.UTF8.GetBytes(providedToken);

        if (expectedBytes.Length != providedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}