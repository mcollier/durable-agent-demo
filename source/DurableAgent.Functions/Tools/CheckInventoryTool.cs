using System.ComponentModel;

using DurableAgent.Functions.Services;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Returns available inventory quantity for a given SKU.
/// </summary>
public static class CheckInventoryTool
{
    [Description("Returns the quantity currently available in inventory for the given SKU. Call this for every SKU in the order to determine fulfillability.")]
    public static int CheckInventory(
        [Description("The stock-keeping unit identifier to check inventory for (e.g. 'QCC-TUB').")] string sku)
    {
        return InventoryRepository.GetAvailableQuantity(sku);
    }
}
