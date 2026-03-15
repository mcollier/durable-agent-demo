namespace DurableAgent.Functions.Services;

/// <summary>
/// Central repository for managing frozen yogurt product inventory data.
/// Provides a single source of truth for available stock levels across the application.
/// </summary>
public static class InventoryRepository
{
    private const string TubSkuSuffix = "-TUB";

    /// <summary>
    /// Converts a canonical FlavorId into the inventory SKU used by the stock system.
    /// </summary>
    /// <param name="flavorId">The canonical three-letter flavor identifier.</param>
    public static string GetSkuForFlavorId(string flavorId)
    {
        ArgumentNullException.ThrowIfNull(flavorId);

        return $"{flavorId.Trim().ToUpperInvariant()}{TubSkuSuffix}";
    }

    /// <summary>
    /// Returns the quantity available in inventory for the given FlavorId, or 0 if the FlavorId is unknown.
    /// </summary>
    /// <param name="flavorId">The canonical three-letter flavor identifier to look up.</param>
    public static int GetAvailableQuantityForFlavorId(string flavorId)
    {
        return GetAvailableQuantity(GetSkuForFlavorId(flavorId));
    }

    /// <summary>
    /// Returns the quantity available in inventory for the given SKU, or 0 if the SKU is unknown.
    /// </summary>
    /// <param name="sku">The stock-keeping unit identifier to look up.</param>
    public static int GetAvailableQuantity(string sku)
    {
        ArgumentNullException.ThrowIfNull(sku);

        return Inventory.TryGetValue(sku.ToUpperInvariant(), out int qty) ? qty : 0;
    }

    /// <summary>
    /// Returns all available inventory items where the quantity in stock is greater than zero.
    /// </summary>
    /// <returns>A dictionary mapping SKU to available quantity, excluding out-of-stock items.</returns>
    public static Dictionary<string, int> GetAllAvailableInventory()
    {
        return Inventory.Where(kvp => kvp.Value > 0)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    // In a real implementation this would query a database or inventory service.
    private static readonly Dictionary<string, int> Inventory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RRS-TUB"] = 8,   // Recursive Raspberry
        ["VNE-TUB"] = 15,  // Vanilla Exception
        ["MNC-TUB"] = 0,   // Mint Condition — out of stock
        ["BBC-TUB"] = 5,   // Berry Blockchain Blast
        ["CCN-TUB"] = 3,   // Cookie Container
        ["NPP-TUB"] = 7,   // Null Pointer Pistachio
        ["JJT-TUB"] = 4,   // Java Jolt
        ["PBP-TUB"] = 6,   // Peanut Butter Protocol
        ["CCC-TUB"] = 9,   // Cloud Caramel Cache
        ["AIA-TUB"] = 12,  // AIçaí Bowl
    };
}
