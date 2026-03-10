namespace DurableAgent.Functions.Services;

/// <summary>
/// Central repository for managing frozen yogurt product inventory data.
/// Provides a single source of truth for available stock levels across the application.
/// </summary>
public static class InventoryRepository
{
    /// <summary>
    /// Returns the quantity available in inventory for the given SKU, or 0 if the SKU is unknown.
    /// </summary>
    /// <param name="sku">The stock-keeping unit identifier to look up.</param>
    public static int GetAvailableQuantity(string sku)
    {
        return Inventory.TryGetValue(sku.ToUpperInvariant(), out int qty) ? qty : 0;
    }

    // In a real implementation this would query a database or inventory service.
    private static readonly Dictionary<string, int> Inventory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QCC-TUB"] = 1,   // Quantum Cookie Crumble — limited stock
        ["AAL-TUB"] = 10,  // Async Almond
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
