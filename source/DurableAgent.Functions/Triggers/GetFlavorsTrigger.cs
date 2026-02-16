using System.Net;
using System.Text.Json;
using DurableAgent.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// HTTP GET endpoint that returns all available frozen yogurt flavors.
/// </summary>
public sealed class GetFlavorsTrigger(ILogger<GetFlavorsTrigger> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly Flavor[] Flavors =
    [
        new() { FlavorId = "flv-001", Name = "Mint Condition", Category = "Classic", ContainsDairy = true, ContainsNuts = false, Description = "Cool mint with dark chocolate chips. Zero bugs detected." },
        new() { FlavorId = "flv-002", Name = "Berry Blockchain Blast", Category = "Fruit", ContainsDairy = true, ContainsNuts = false, Description = "Strawberry + raspberry layered immutably for distributed sweetness." },
        new() { FlavorId = "flv-003", Name = "Cookie Container", Category = "Dessert", ContainsDairy = true, ContainsNuts = false, Description = "Chocolate cookie crumble isolated in its own delicious container." },
        new() { FlavorId = "flv-004", Name = "Recursive Raspberry", Category = "Fruit", ContainsDairy = false, ContainsNuts = false, Description = "Raspberry that calls itself again. And again. And again." },
        new() { FlavorId = "flv-005", Name = "Vanilla Exception", Category = "Classic", ContainsDairy = true, ContainsNuts = false, Description = "Simple. Predictable. Until it isn't." },
        new() { FlavorId = "flv-006", Name = "Null Pointer Pistachio", Category = "Nutty", ContainsDairy = true, ContainsNuts = true, Description = "Rich pistachio with zero reference errors." },
        new() { FlavorId = "flv-007", Name = "Java Jolt", Category = "Coffee", ContainsDairy = true, ContainsNuts = false, Description = "Strong coffee base compiled for performance." },
        new() { FlavorId = "flv-008", Name = "Peanut Butter Protocol", Category = "Nutty", ContainsDairy = true, ContainsNuts = true, Description = "A well-defined interface between chocolate and peanut butter." },
        new() { FlavorId = "flv-009", Name = "Cloud Caramel Cache", Category = "Dessert", ContainsDairy = true, ContainsNuts = false, Description = "Warm caramel layered for fast retrieval." },
        new() { FlavorId = "flv-010", Name = "AIçaí Bowl", Category = "Fruit", ContainsDairy = false, ContainsNuts = false, Description = "Smarter acai with machine-learned flavor balance." },
    ];

    [Function(nameof(GetFlavorsTrigger))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "flavors")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Retrieving list of all flavors.");

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(Flavors, JsonOptions), cancellationToken);

        return response;
    }
}
