// create a class to represent the result of the fulfillment decision
// example json
/*
{
                            "orderId": "string",
                            "customerEmail": "string",
                            "items": [
                                {
                                    "sku": "string",
                                    "productName": "string",
                                    "requestedQty": 0,
                                    "availableQty": 0,
                                    "fulfillableQty": 0,
                                    "shortfallQty": 0
                                }
                            ],
                            "canFullyFulfill": false,
                            "shouldGenerateCoupon": false,
                            "coupon": {
                                "code": "string",
                                "discountPercent": 0
                            },
                            "alternativeRecommendations": [
                                {
                                    "sku": "string",
                                    "productName": "string"
                                }
                            ]
                        }
*/
namespace DurableAgent.Functions.Models;

/// <summary>
/// Represents the result of the fulfillment decision for an order.
/// </summary>
public sealed record FulfillmentDecisionResult
{
    public required string OrderId { get; init; }   
    public required string CustomerEmail { get; init; }
    public IReadOnlyList<FulfillmentDecisionLineItem> Items { get; init; } = [];
    public required bool CanFullyFulfill { get; init; }
    public required bool ShouldGenerateCoupon { get; init; }
    public Coupon? Coupon { get; init; }
    public IReadOnlyList<AlternativeRecommendation> AlternativeRecommendations { get; init; } = [];
}   

public sealed record FulfillmentDecisionLineItem
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