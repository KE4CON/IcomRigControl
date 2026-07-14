using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake HttpMessageHandler that returns different canned responses based
/// on whether the request looks like a login or a callsign query, so we can
/// test QrzLookupSource's two-step session flow without a real network call.
/// </summary>
public class FakeQrzHandler : HttpMessageHandler
{
    public string LoginResponse { get; set; } = "";
    public string CallsignResponse { get; set; } = "";
    public int LoginCallCount { get; private set; }
    public int CallsignCallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken ct)
    {
        var url = request.RequestUri?.ToString() ?? "";
        string body;

        if (url.Contains("username="))
        {
            LoginCallCount++;
            body = LoginResponse;
        }
        else
        {
            CallsignCallCount++;
            body = CallsignResponse;
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };
        return Task.FromResult(response);
    }
}

public class QrzLookupSourceTests
{
    private const string SuccessfulLoginXml = @"<?xml version=""1.0"" ?>
<QRZDatabase version=""1.34"" xmlns=""http://xmldata.qrz.com"">
<Session>
<Key>abc123sessionkey</Key>
<Count>1</Count>
<GMTime>Tue Jul 14 20:00:00 2026</GMTime>
</Session>
</QRZDatabase>";

    private const string FailedLoginXml = @"<?xml version=""1.0"" ?>
<QRZDatabase version=""1.34"" xmlns=""http://xmldata.qrz.com"">
<Session>
<Error>Username/password incorrect</Error>
<GMTime>Tue Jul 14 20:00:00 2026</GMTime>
</Session>
</QRZDatabase>";

    private const string SuccessfulCallsignXml = @"<?xml version=""1.0"" ?>
<QRZDatabase version=""1.34"" xmlns=""http://xmldata.qrz.com"">
<Callsign>
<call>W1AW</call>
<fname>ARRL</fname>
<name>HQ OPERATORS CLUB</name>
<addr1>225 MAIN ST</addr1>
<addr2>NEWINGTON</addr2>
<state>CT</state>
<grid>FN31pr</grid>
<class>E</class>
</Callsign>
<Session>
<Key>abc123sessionkey</Key>
<GMTime>Tue Jul 14 20:00:01 2026</GMTime>
</Session>
</QRZDatabase>";

    private const string NotFoundCallsignXml = @"<?xml version=""1.0"" ?>
<QRZDatabase version=""1.34"" xmlns=""http://xmldata.qrz.com"">
<Session>
<Error>Not found: BADCALL</Error>
<Key>abc123sessionkey</Key>
<GMTime>Tue Jul 14 20:00:02 2026</GMTime>
</Session>
</QRZDatabase>";

    [Fact]
    public async Task Lookup_ValidCallsign_ReturnsCallsignInfo()
    {
        var handler = new FakeQrzHandler { LoginResponse = SuccessfulLoginXml, CallsignResponse = SuccessfulCallsignXml };
        var httpClient = new HttpClient(handler);
        var source = new QrzLookupSource(httpClient, "testuser", "testpass");

        var result = await source.LookupAsync("W1AW");

        Assert.NotNull(result);
        Assert.Equal("W1AW", result!.Callsign);
        Assert.Contains("ARRL", result.Name);
        Assert.Equal("FN31pr", result.GridSquare);
    }

    [Fact]
    public async Task Lookup_LogsInOnlyOnce_ReusesSessionForSubsequentLookups()
    {
        var handler = new FakeQrzHandler { LoginResponse = SuccessfulLoginXml, CallsignResponse = SuccessfulCallsignXml };
        var httpClient = new HttpClient(handler);
        var source = new QrzLookupSource(httpClient, "testuser", "testpass");

        await source.LookupAsync("W1AW");
        await source.LookupAsync("W1AW");

        Assert.Equal(1, handler.LoginCallCount);
        Assert.Equal(2, handler.CallsignCallCount);
    }

    [Fact]
    public async Task Lookup_BadCredentials_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeQrzHandler { LoginResponse = FailedLoginXml };
        var httpClient = new HttpClient(handler);
        var source = new QrzLookupSource(httpClient, "wronguser", "wrongpass");

        var result = await source.LookupAsync("W1AW");

        Assert.Null(result);
    }

    [Fact]
    public async Task Lookup_CallsignNotFound_ReturnsNull()
    {
        var handler = new FakeQrzHandler { LoginResponse = SuccessfulLoginXml, CallsignResponse = NotFoundCallsignXml };
        var httpClient = new HttpClient(handler);
        var source = new QrzLookupSource(httpClient, "testuser", "testpass");

        var result = await source.LookupAsync("BADCALL");

        Assert.Null(result);
    }

    [Fact]
    public async Task Lookup_MalformedXml_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeQrzHandler { LoginResponse = "not xml <<<" };
        var httpClient = new HttpClient(handler);
        var source = new QrzLookupSource(httpClient, "testuser", "testpass");

        var result = await source.LookupAsync("W1AW");

        Assert.Null(result);
    }

    [Fact]
    public void SourceName_IsQrz()
    {
        var source = new QrzLookupSource(new HttpClient(), "u", "p");
        Assert.Equal("QRZ.com", source.SourceName);
    }

    [Fact]
    public void RequiresCredentials_IsTrue()
    {
        var source = new QrzLookupSource(new HttpClient(), "u", "p");
        Assert.True(source.RequiresCredentials);
    }
}