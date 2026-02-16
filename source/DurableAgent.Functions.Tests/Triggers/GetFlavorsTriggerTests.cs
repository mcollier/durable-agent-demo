using System.Net;
using System.Text.Json;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Tests.TestHelpers;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class GetFlavorsTriggerTests
{
    private readonly ILogger<GetFlavorsTrigger> _logger = A.Fake<ILogger<GetFlavorsTrigger>>();
    private readonly FunctionContext _functionContext = A.Fake<FunctionContext>();

    [Fact]
    public async Task WhenCalled_ThenReturns200WithFlavorsList()
    {
        var trigger = new GetFlavorsTrigger(_logger);
        var request = new FakeHttpRequestData(_functionContext, method: "GET", url: "http://localhost/api/flavors");

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var fakeResponse = (FakeHttpResponseData)response;
        var body = fakeResponse.ReadBodyAsString();
        var flavors = JsonSerializer.Deserialize<Flavor[]>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        Assert.NotNull(flavors);
        Assert.Equal(10, flavors.Length);
        Assert.Contains(flavors, f => f.FlavorId == "flv-001" && f.Name == "Mint Condition");
        Assert.Contains(flavors, f => f.FlavorId == "flv-002" && f.Name == "Berry Blockchain Blast");
        Assert.Contains(flavors, f => f.FlavorId == "flv-010" && f.Name == "AIçaí Bowl");
    }

    [Fact]
    public async Task WhenCalled_ThenResponseIsValidJson()
    {
        var trigger = new GetFlavorsTrigger(_logger);
        var request = new FakeHttpRequestData(_functionContext, method: "GET", url: "http://localhost/api/flavors");

        var response = await trigger.RunAsync(request, CancellationToken.None);

        var fakeResponse = (FakeHttpResponseData)response;
        var body = fakeResponse.ReadBodyAsString();
        
        // Should not throw
        var flavors = JsonSerializer.Deserialize<Flavor[]>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(flavors);
    }

    [Fact]
    public async Task WhenCalled_ThenSetsContentTypeHeader()
    {
        var trigger = new GetFlavorsTrigger(_logger);
        var request = new FakeHttpRequestData(_functionContext, method: "GET", url: "http://localhost/api/flavors");

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.True(response.Headers.TryGetValues("Content-Type", out var contentTypes));
        Assert.Contains("application/json", contentTypes);
    }

    [Fact]
    public async Task WhenRequestIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new GetFlavorsTrigger(_logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }
}
