using System.ComponentModel;
using System.Text.Json;
using DurableAgent.Functions.Services;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Retrieves store details by store ID for the AI agent.
/// </summary>
public static class GetStoreDetailsTool
{
    [Description("Gets details for a specific store by store ID. Returns JSON with store name, address, phone, email, manager, and timezone.")]
    public static string GetStoreDetails([Description("The ID of the store to retrieve details for.")] string storeId)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var store = StoreRepository.GetStoreById(storeId);

        return store is not null
            ? JsonSerializer.Serialize(store, jsonOptions)
            : JsonSerializer.Serialize(new { error = "Store not found", storeId }, jsonOptions);
    }
}
