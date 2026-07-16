using System.Text;
using IcomRigControl.RigModel;
using Xunit;

namespace IcomRigControl.Tests;

public class CivNetworkProtocolTests
{
    [Fact]
    public void BuildAuthRequest_IncludesToken()
    {
        byte[] request = CivNetworkProtocol.BuildAuthRequest("mysecrettoken");
        string text = Encoding.UTF8.GetString(request);

        Assert.Contains("mysecrettoken", text);
        Assert.StartsWith("AUTH:", text);
    }

    [Fact]
    public void TryParseAuthRequest_ValidRequest_ExtractsToken()
    {
        byte[] request = CivNetworkProtocol.BuildAuthRequest("mysecrettoken");

        bool success = CivNetworkProtocol.TryParseAuthRequest(request, out string? token);

        Assert.True(success);
        Assert.Equal("mysecrettoken", token);
    }

    [Fact]
    public void TryParseAuthRequest_InvalidData_ReturnsFalse()
    {
        byte[] junk = Encoding.UTF8.GetBytes("not an auth request at all");

        bool success = CivNetworkProtocol.TryParseAuthRequest(junk, out string? token);

        Assert.False(success);
        Assert.Null(token);
    }

    [Fact]
    public void AuthSuccessResponse_IsRecognized()
    {
        byte[] response = CivNetworkProtocol.AuthSuccessResponse;
        Assert.True(CivNetworkProtocol.IsAuthSuccess(response));
    }

    [Fact]
    public void AuthFailureResponse_IsNotRecognizedAsSuccess()
    {
        byte[] response = CivNetworkProtocol.AuthFailureResponse;
        Assert.False(CivNetworkProtocol.IsAuthSuccess(response));
    }

    [Fact]
    public void ValidateToken_MatchingTokens_ReturnsTrue()
    {
        bool result = CivNetworkProtocol.ValidateToken("correct-token", "correct-token");
        Assert.True(result);
    }

    [Fact]
    public void ValidateToken_MismatchedTokens_ReturnsFalse()
    {
        bool result = CivNetworkProtocol.ValidateToken("correct-token", "wrong-token");
        Assert.False(result);
    }

    [Fact]
    public void ValidateToken_EmptyExpectedToken_AlwaysReturnsFalse()
    {
        // An empty/unset server-side token should never validate — this
        // prevents accidentally running an unauthenticated server if the
        // configured token was left blank.
        bool result = CivNetworkProtocol.ValidateToken("", "anything");
        Assert.False(result);
    }
}