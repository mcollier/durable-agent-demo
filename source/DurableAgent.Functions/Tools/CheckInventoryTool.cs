using System.ComponentModel;

using DurableAgent.Functions.Services;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Returns available inventory quantity for a canonical FlavorId.
/// </summary>
public static class CheckInventoryTool
{
    [Description("Returns the quantity currently available in inventory for the given canonical FlavorId. The tool converts the FlavorId to the corresponding inventory SKU internally.")]
    public static int CheckInventory(
        [Description("The canonical three-letter FlavorId to check inventory for (e.g. 'VNE').")] string flavorId)
    {
        ArgumentNullException.ThrowIfNull(flavorId);

        return InventoryRepository.GetAvailableQuantityForFlavorId(flavorId);
    }
}
