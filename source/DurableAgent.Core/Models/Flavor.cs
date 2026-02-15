namespace DurableAgent.Core.Models;

/// <summary>
/// Represents a frozen yogurt flavor offered by Froyo Foundry.
/// </summary>
public sealed record Flavor
{
    /// <summary>Unique identifier for the flavor (e.g., "flv-001").</summary>
    public required string FlavorId { get; init; }

    /// <summary>Display name of the flavor.</summary>
    public required string Name { get; init; }

    /// <summary>Flavor category (e.g., Classic, Fruit, Dessert, Nutty, Coffee).</summary>
    public required string Category { get; init; }

    /// <summary>Whether the flavor contains dairy ingredients.</summary>
    public required bool ContainsDairy { get; init; }

    /// <summary>Whether the flavor contains nut ingredients.</summary>
    public required bool ContainsNuts { get; init; }

    /// <summary>Short description of the flavor.</summary>
    public required string Description { get; init; }
}
