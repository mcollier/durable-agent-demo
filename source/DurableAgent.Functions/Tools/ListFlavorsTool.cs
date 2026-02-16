using System.ComponentModel;
using System.Text.Json;
using DurableAgent.Functions.Services;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Lists all available frozen yogurt flavors for the AI agent.
/// </summary>
public static class ListFlavorsTool
{
    [Description("Lists all available frozen yogurt flavors with allergen information.")]
    public static string ListFlavors()
    {
        var flavors = FlavorRepository.GetAllFlavors();
        return JsonSerializer.Serialize(flavors, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
