namespace DurableAgent.Core.Models;

/// <summary>
/// Represents the inventory analysis output for an order, including shortfall details and any issued coupon.
/// </summary>
public sealed record InventoryAnalysisResult
{
    /// <summary>Unique identifier for the order.</summary>
    public required string OrderId { get; init; }

    /// <summary>Customer's email address for follow-up communication.</summary>
    public required string CustomerEmail { get; init; }

    /// <summary>Line-item fulfillment details for each ordered product.</summary>
    public IReadOnlyList<InventoryAnalysisItem> Items { get; init; } = [];

    /// <summary>True if every item in the order can be fully fulfilled.</summary>
    public required bool CanFullyFulfill { get; init; }

    /// <summary>True if a coupon should be generated for this order.</summary>
    public required bool ShouldGenerateCoupon { get; init; }

    /// <summary>Coupon issued to the customer, if applicable.</summary>
    public InventoryAnalysisCoupon? Coupon { get; init; }
}

/// <summary>
/// Inventory analysis details for a single line item in an order.
/// </summary>
public sealed record InventoryAnalysisItem
{
    /// <summary>Stock-keeping unit identifier.</summary>
    public required string Sku { get; init; }

    /// <summary>Human-readable product name.</summary>
    public required string ProductName { get; init; }

    /// <summary>Quantity originally requested by the customer.</summary>
    public required int RequestedQty { get; init; }

    /// <summary>Quantity currently available in inventory.</summary>
    public required int AvailableQty { get; init; }

    /// <summary>Quantity that can actually be fulfilled (min of requested and available).</summary>
    public required int FulfillableQty { get; init; }

    /// <summary>Quantity that cannot be fulfilled (requestedQty − fulfillableQty).</summary>
    public required int ShortfallQty { get; init; }
}

/// <summary>
/// Coupon issued as part of an inventory analysis decision.
/// </summary>
public sealed record InventoryAnalysisCoupon
{
    /// <summary>Coupon code for the customer to redeem.</summary>
    public required string Code { get; init; }

    /// <summary>Discount percentage (e.g., 25 for 25%).</summary>
    public required int DiscountPercent { get; init; }
}
