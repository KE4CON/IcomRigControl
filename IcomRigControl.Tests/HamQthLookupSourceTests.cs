using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake HttpMessageHandler that returns different canned responses based
/// on whether the request looks like a login (contains "u=") or a callsign
/// query, so we can test HamQthLookupSource's session flow without a real
/// network call.
/// </summary>
public class FakeHamQthHandler : HttpMessageHandler
{
    public string LoginResponse { get; set; } = "";
    public string CallsignResponse { get; set; } = "";
    public int LoginCallCount { get; private set; }
    public int CallsignCallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken ct)
    {
        var url = request.RequestUri?.ToString() ?? "";
        string body;

        if (url.Contains("u=") && url.Contains("p="))
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

public class HamQthLookupSourceTests
{
    private const string SuccessfulLoginXml = @"<?xml version=""1.0""?>
<HamQTH version=""2.7"" xmlns=""https://www.hamqth.com"">
<session>
<session_id>09b0ae90050be03c452ad235a1f2915ad684393c</session_id>
</session>
</HamQTH>";

    private const string FailedLoginXml = @"<?xml version=""1.0""?>
<HamQTH version=""2.7"" xmlns=""https://www.hamqth.com"">
<session>
<error>Wrong user name or password</error>
</session>
</HamQTH>";

    private const string SuccessfulCallsignXml = @"<?xml version=""1.0""?>
<HamQTH version=""2.7"" xmlns=""https://www.hamqth.com"">
<search>
<callsign>ok2cqr</callsign>
<nick>Petr</nick>
<qth>Neratovice</qth>
<country>Czech Republic</country>
<grid>jo70gg</grid>
<adr_name>Petr Hlozek</adr_name>
<adr_street1>17. listopadu 1065</adr_street1>
<adr_city>Neratovice</adr_city>
</search>
</HamQTH>";

    private const string NotFoundXml = @"<?xml version=""1.0""?>
<HamQTH version=""2.7"" xmlns=""https://www.hamqth.com"">
<session>
<error>Callsign not found</error>
</session>
</HamQTH>";

    [Fact]
    public async Task Lookup_ValidCallsign_ReturnsCallsignInfo()
    {
        var handler = new FakeHamQthHandler { LoginResponse = SuccessfulLoginXml, CallsignResponse = SuccessfulCallsignXml };
        var httpClient = new HttpClient(handler);
        var source = new HamQthLookupSource(httpClient, "testuser", "testpass");

        var result = await source.LookupAsync("OK2CQR");

        Assert.NotNull(result);
        Assert.Equal("ok2cqr", result!.Callsign);
        Assert.Equal("Petr Hlozek", result.Name);
        Assert.Equal("jo70gg", result.GridSquare);
    }

    [Fact]
    public async Task Lookup_LogsInOnlyOnce_ReusesSessionForSubsequentLookups()
    {
        var handler = new FakeHamQthHandler { LoginResponse = SuccessfulLoginXml, CallsignResponse = SuccessfulCallsignXml };
        var httpClient = new HttpClient(handler);
        var source = new HamQthLookupSource(httpClient, "testuser", "testpass");

        await source.LookupAsync("OK2CQR");
        await source.LookupAsync("OK2CQR");

        Assert.Equal(1, handler.LoginCallCount);
        Assert.Equal(2, handler.CallsignCallCount);
    }

    [Fact]
    public async Task Lookup_BadCredentials_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeHamQthHandler { LoginResponse = FailedLoginXml };
        var httpClient = new HttpClient(handler);
        var source = new HamQthLookupSource(httpClient, "wronguser", "wrongpass");

        var result = await source.LookupAsync("OK2CQR");

        Assert.Null(result);
    }

    [Fact]
    public async Task Lookup_CallsignNotFound_ReturnsNull()
    {
        var handler = new FakeHamQthHandler { LoginResponse = SuccessfulLoginXml, CallsignResponse = NotFoundXml };
        var httpClient = new HttpClient(handler);
        var source = new HamQthLookupSource(httpClient, "testuser", "testpass");

        var result = await source.LookupAsync("BADCALL");

        Assert.Null(result);
    }

    [Fact]
    public async Task Lookup_MalformedXml_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeHamQthHandler { LoginResponse = "not xml <<<" };
        var httpClient = new HttpClient(handler);
        var source = new HamQthLookupSource(httpClient, "testuser", "testpass");

        var result = await source.LookupAsync("OK2CQR");

        Assert.Null(result);
    }

    [Fact]
    public void SourceName_IsHamQth()
    {
        var source = new HamQthLookupSource(new HttpClient(), "u", "p");
        Assert.Equal("HamQTH.com", source.SourceName);
    }

    [Fact]
    public void RequiresCredentials_IsTrue()
    {
        // HamQTH is free, but does require a registered (free) account.
        var source = new HamQthLookupSource(new HttpClient(), "u", "p");
        Assert.True(source.RequiresCredentials);
    }
}