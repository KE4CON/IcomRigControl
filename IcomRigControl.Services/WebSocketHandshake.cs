using System.Security.Cryptography;
using System.Text;

namespace IcomRigControl.Services;

/// <summary>
/// The one non-obvious bit of the WebSocket (RFC 6455) opening handshake: the
/// server proves it understood the upgrade by hashing the client's
/// Sec-WebSocket-Key with a fixed GUID and returning it as Sec-WebSocket-Accept.
/// Kept separate so it can be unit-tested against the RFC's worked example. Once
/// the handshake is written to the socket, the rest is handled by the framework's
/// WebSocket.CreateFromStream. See CLAUDE.md web remote.
/// </summary>
public static class WebSocketHandshake
{
    private const string MagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    public static string ComputeAcceptKey(string secWebSocketKey)
    {
        byte[] hash = SHA1.HashData(Encoding.ASCII.GetBytes(secWebSocketKey + MagicGuid));
        return Convert.ToBase64String(hash);
    }
}
