using System.Net;
using System.Text.Json;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Tests.TestHelpers;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class GetStoresTriggerTests
{
    private readonly ILogger<GetStoresTrigger> _logger = A.Fake<ILogger<GetStoresTrigger>>();
    private readonly FunctionContext _functionContext = A.Fake<FunctionContext>();

    [Fact]
    public async Task WhenCalled_ThenReturns200WithStoresList()
    {
        var trigger = new GetStoresTrigger(_logger);
        var request = new FakeHttpRequestData(_functionContext, method: "GET", url: "http://localhost/api/stores");

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var fakeResponse = (FakeHttpResponseData)response;
        var body = fakeResponse.ReadBodyAsString();
        var stores = JsonSerializer.Deserialize<Store[]>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        Assert.NotNull(stores);
        Assert.Equal(5, stores.Length);
        Assert.Contains(stores, s => s.StoreId == "store-001" && s.Name == "Froyo Foundry - Hilliard");
        Assert.Contains(stores, s => s.StoreId == "store-002" && s.Name == "Froyo Foundry - Dublin");
        Assert.Contains(stores, s => s.StoreId == "store-003" && s.Name == "Froyo Foundry - Easton");
        Assert.Contains(stores, s => s.StoreId == "store-004" && s.Name == "Froyo Foundry - Short North");
        Assert.Contains(stores, s => s.StoreId == "store-005" && s.Name == "Froyo Foundry - Polaris");
    }

    [Fact]
    public async Task WhenCalled_ThenResponseIsValidJson()
    {
        var trigger = new GetStoresTrigger(_logger);
        var request = new FakeHttpRequestData(_functionContext, method: "GET", url: "http://localhost/api/stores");

        var response = await trigger.RunAsync(request, CancellationToken.None);

        var fakeResponse = (FakeHttpResponseData)response;
        var body = fakeResponse.ReadBodyAsString();
        
        // Should not throw
        var stores = JsonSerializer.Deserialize<Store[]>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(stores);
    }

    [Fact]
    public async Task WhenCalled_ThenSetsContentTypeHeader()
    {
        var trigger = new GetStoresTrigger(_logger);
        var request = new FakeHttpRequestData(_functionContext, method: "GET", url: "http://localhost/api/stores");

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.True(response.Headers.TryGetValues("Content-Type", out var contentTypes));
        Assert.Contains("application/json", contentTypes);
    }

    [Fact]
    public async Task WhenRequestIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new GetStoresTrigger(_logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }
}
