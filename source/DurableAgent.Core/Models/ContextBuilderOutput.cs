namespace DurableAgent.Core.Models;

/// <summary>
/// Structured context assembled by the Context Builder Agent for downstream substitution planning.
/// </summary>
public sealed record ContextBuilderOutput
{
    /// <summary>Core order metadata.</summary>
    public required OrderContext Order { get; init; }

    /// <summary>Details describing the inventory shortfall.</summary>
    public required ShortfallContext Shortfall { get; init; }

    /// <summary>Product catalog data for candidate substitute products.</summary>
    public required CatalogContext CatalogContext { get; init; }

    /// <summary>Warehouse inventory levels for candidate substitute products.</summary>
    public required InventoryContext InventoryContext { get; init; }

    /// <summary>Business rules governing substitution and incentives.</summary>
    public required PolicyContext PolicyContext { get; init; }

    /// <summary>Fields that could not be retrieved.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = [];

    /// <summary>Short factual notes about data quality or limitations.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// Basic order metadata.
/// </summary>
public sealed record OrderContext
{
    /// <summary>Unique identifier for the order.</summary>
    public required string OrderId { get; init; }

    /// <summary>Unique identifier for the customer.</summary>
    public required string CustomerId { get; init; }
}

/// <summary>
/// Details describing the inventory shortfall for the impacted SKU.
/// </summary>
public sealed record ShortfallContext
{
    /// <summary>Stock-keeping unit identifier for the impacted product.</summary>
    public required string Sku { get; init; }

    /// <summary>Human-readable product name.</summary>
    public required string Name { get; init; }

    /// <summary>Quantity originally requested by the customer.</summary>
    public required int RequestedQty { get; init; }

    /// <summary>Quantity that could be reserved from available stock.</summary>
    public required int ReservedQty { get; init; }

    /// <summary>Quantity that is missing (requestedQty − reservedQty).</summary>
    public required int MissingQty { get; init; }
}

/// <summary>
/// Product catalog data for candidate substitute products.
/// </summary>
public sealed record CatalogContext
{
    /// <summary>Candidate substitute products from the catalog.</summary>
    public IReadOnlyList<CandidateProduct> CandidateProducts { get; init; } = [];
}

/// <summary>
/// A single candidate substitute product from the catalog.
/// </summary>
public sealed record CandidateProduct
{
    /// <summary>Stock-keeping unit identifier.</summary>
    public required string Sku { get; init; }

    /// <summary>Human-readable product name.</summary>
    public required string Name { get; init; }

    /// <summary>Flavor tags describing the product's taste profile.</summary>
    public IReadOnlyList<string> FlavorTags { get; init; } = [];
}

/// <summary>
/// Warehouse inventory levels for candidate substitute products.
/// </summary>
public sealed record InventoryContext
{
    /// <summary>Identifier of the warehouse providing inventory data.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>Available inventory for each candidate substitute SKU.</summary>
    public IReadOnlyList<CandidateInventory> CandidateInventory { get; init; } = [];
}

/// <summary>
/// Inventory availability for a single candidate substitute SKU.
/// </summary>
public sealed record CandidateInventory
{
    /// <summary>Stock-keeping unit identifier.</summary>
    public required string Sku { get; init; }

    /// <summary>Quantity available in the warehouse.</summary>
    public required int AvailableQty { get; init; }
}

/// <summary>
/// Business rules governing substitution and incentives.
/// </summary>
public sealed record PolicyContext
{
    /// <summary>Whether substitution is permitted for this order.</summary>
    public required bool SubstitutionAllowed { get; init; }

    /// <summary>Maximum discount percentage that may be offered as an incentive.</summary>
    public required int MaxDiscountPercent { get; init; }
}
