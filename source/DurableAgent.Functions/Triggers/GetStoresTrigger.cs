using System.Net;
using System.Text.Json;
using DurableAgent.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// HTTP GET endpoint that returns all available Froyo Foundry store locations.
/// </summary>
public sealed class GetStoresTrigger(ILogger<GetStoresTrigger> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Function(nameof(GetStoresTrigger))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Retrieving list of all stores.");

        var stores = StoreRepository.GetAllStores();

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(stores, JsonOptions), cancellationToken);

        return response;
    }
}
