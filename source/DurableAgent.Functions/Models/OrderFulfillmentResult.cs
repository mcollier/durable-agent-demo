namespace DurableAgent.Functions.Models;

/// <summary>
/// Represents the validated fulfillment outcome for an order.
/// </summary>
public sealed record OrderFulfillmentResult
{
    public required bool IsValidOrder { get; init; }
    public string? ValidationError { get; init; }
    public string? OrderId { get; init; }
    public string? CustomerEmail { get; init; }
    public IReadOnlyList<OrderFulfillmentLineItem> Items { get; init; } = [];
    public required bool CanFullyFulfill { get; init; }
    public Coupon? Coupon { get; init; }
    public IReadOnlyList<AlternativeRecommendation> AlternativeRecommendations { get; init; } = [];
}

public sealed record OrderFulfillmentLineItem
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required int RequestedQty { get; init; }
    public required int AvailableQty { get; init; }
    public required int FulfillableQty { get; init; }
    public required int ShortfallQty { get; init; }
}

public sealed record Coupon
{
    public required string Code { get; init; }
    public required int DiscountPercent { get; init; }
}

public sealed record AlternativeRecommendation
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
}
