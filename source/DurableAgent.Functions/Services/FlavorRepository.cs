using DurableAgent.Core.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Central repository for managing frozen yogurt flavor data.
/// Provides a single source of truth for all flavor information across the application.
/// </summary>
public static class FlavorRepository
{
    /// <summary>
    /// Gets all available flavors.
    /// </summary>
    public static IReadOnlyList<Flavor> GetAllFlavors() => Array.AsReadOnly(Flavors);

    /// <summary>
    /// Gets a specific flavor by its ID.
    /// </summary>
    /// <param name="flavorId">The unique identifier of the flavor.</param>
    /// <returns>The flavor if found, otherwise null.</returns>
    public static Flavor? GetFlavorById(string flavorId)
    {
        return Array.Find(Flavors, f => f.FlavorId.Equals(flavorId, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Flavor[] Flavors =
    [
        new() { FlavorId = "MNC", Name = "Mint Condition", Category = "Classic", ContainsDairy = true, ContainsNuts = false, Description = "Cool mint with dark chocolate chips. Zero bugs detected." },
        new() { FlavorId = "BBC", Name = "Berry Blockchain Blast", Category = "Fruit", ContainsDairy = true, ContainsNuts = false, Description = "Strawberry + raspberry layered immutably for distributed sweetness." },
        new() { FlavorId = "CCN", Name = "Cookie Container", Category = "Dessert", ContainsDairy = true, ContainsNuts = false, Description = "Chocolate cookie crumble isolated in its own delicious container." },
        new() { FlavorId = "RRS", Name = "Recursive Raspberry", Category = "Fruit", ContainsDairy = false, ContainsNuts = false, Description = "Raspberry that calls itself again. And again. And again." },
        new() { FlavorId = "VNE", Name = "Vanilla Exception", Category = "Classic", ContainsDairy = true, ContainsNuts = false, Description = "Simple. Predictable. Until it isn't." },
        new() { FlavorId = "NPP", Name = "Null Pointer Pistachio", Category = "Nutty", ContainsDairy = true, ContainsNuts = true, Description = "Rich pistachio with zero reference errors." },
        new() { FlavorId = "JJT", Name = "Java Jolt", Category = "Coffee", ContainsDairy = true, ContainsNuts = false, Description = "Strong coffee base compiled for performance." },
        new() { FlavorId = "PBP", Name = "Peanut Butter Protocol", Category = "Nutty", ContainsDairy = true, ContainsNuts = true, Description = "A well-defined interface between chocolate and peanut butter." },
        new() { FlavorId = "CCC", Name = "Cloud Caramel Cache", Category = "Dessert", ContainsDairy = true, ContainsNuts = false, Description = "Warm caramel layered for fast retrieval." },
        new() { FlavorId = "AIA", Name = "AIçaí Bowl", Category = "Fruit", ContainsDairy = false, ContainsNuts = false, Description = "Smarter acai with machine-learned flavor balance." },
    ];
}
