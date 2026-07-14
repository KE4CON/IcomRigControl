using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IcomRigControl.Services;
using Xunit;

namespace IcomRigControl.Tests;

/// <summary>
/// A fake HttpMessageHandler that returns a canned response instead of
/// making a real network call, so lookup sources can be tested offline.
/// </summary>
public class FakeHttpResponseHandler : HttpMessageHandler
{
    private readonly string _responseBody;
    private readonly HttpStatusCode _statusCode;

    public FakeHttpResponseHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = responseBody;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken ct)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };
        return Task.FromResult(response);
    }
}

public class CallookLookupSourceTests
{
    private const string SampleValidResponse = @"{
        ""status"": ""VALID"",
        ""type"": ""CLUB"",
        ""current"": { ""callsign"": ""W1AW"", ""operClass"": """" },
        ""trustee"": { ""callsign"": ""K1ZZ"", ""name"": ""SUMNER, DAVID G"" },
        ""name"": ""ARRL HQ OPERATORS CLUB"",
        ""address"": { ""line1"": ""225 MAIN ST"", ""line2"": ""NEWINGTON, CT 06111"", ""attn"": """" },
        ""location"": { ""latitude"": ""41.714776"", ""longitude"": ""-72.726744"", ""gridsquare"": ""FN31pr"" },
        ""otherInfo"": { ""grantDate"": ""12/02/2010"", ""expiryDate"": ""02/26/2021"", ""lastActionDate"": ""12/02/2010"", ""frn"": ""0004511143"", ""ulsUrl"": """" }
    }";

    private const string SampleInvalidResponse = @"{
        ""status"": ""INVALID"",
        ""type"": """",
        ""current"": { ""callsign"": """", ""operClass"": """" },
        ""trustee"": { ""callsign"": """", ""name"": """" },
        ""name"": """",
        ""address"": { ""line1"": """", ""line2"": """", ""attn"": """" },
        ""location"": { ""latitude"": """", ""longitude"": """", ""gridsquare"": """" },
        ""otherInfo"": { ""grantDate"": """", ""expiryDate"": """", ""lastActionDate"": """", ""frn"": """", ""ulsUrl"": """" }
    }";

    [Fact]
    public async Task Lookup_ValidCallsign_ReturnsCallsignInfo()
    {
        var handler = new FakeHttpResponseHandler(SampleValidResponse);
        var httpClient = new HttpClient(handler);
        var source = new CallookLookupSource(httpClient);

        var result = await source.LookupAsync("W1AW");

        Assert.NotNull(result);
        Assert.Equal("W1AW", result!.Callsign);
        Assert.Equal("ARRL HQ OPERATORS CLUB", result.Name);
        Assert.Equal("FN31pr", result.GridSquare);
    }

    [Fact]
    public async Task Lookup_InvalidCallsign_ReturnsNull()
    {
        var handler = new FakeHttpResponseHandler(SampleInvalidResponse);
        var httpClient = new HttpClient(handler);
        var source = new CallookLookupSource(httpClient);

        var result = await source.LookupAsync("BADCALL");

        Assert.Null(result);
    }

    [Fact]
    public async Task Lookup_NetworkFailure_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeHttpResponseHandler("", HttpStatusCode.ServiceUnavailable);
        var httpClient = new HttpClient(handler);
        var source = new CallookLookupSource(httpClient);

        var result = await source.LookupAsync("W1AW");

        Assert.Null(result);
    }

    [Fact]
    public async Task Lookup_MalformedJson_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeHttpResponseHandler("not valid json <<<");
        var httpClient = new HttpClient(handler);
        var source = new CallookLookupSource(httpClient);

        var result = await source.LookupAsync("W1AW");

        Assert.Null(result);
    }

    [Fact]
    public void SourceName_IsCallook()
    {
        var source = new CallookLookupSource(new HttpClient());
        Assert.Equal("Callook.info", source.SourceName);
    }

    [Fact]
    public void RequiresCredentials_IsFalse()
    {
        var source = new CallookLookupSource(new HttpClient());
        Assert.False(source.RequiresCredentials);
    }
}