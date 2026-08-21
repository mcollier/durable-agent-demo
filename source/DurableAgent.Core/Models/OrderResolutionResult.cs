namespace DurableAgent.Core.Models;

/// <summary>
/// Outcome of the Order Resolution handoff sub-flow.
/// </summary>
public enum ResolutionOutcome
{
    /// <summary>The order was resolved by substituting one or more unavailable items.</summary>
    Substituted,

    /// <summary>The order was resolved by offering the customer a promotional incentive.</summary>
    Promoted,

    /// <summary>The order was escalated for human or policy review.</summary>
    Escalated,

    /// <summary>The order could not be resolved autonomously and requires manual handling.</summary>
    Unresolvable
}

/// <summary>
/// Represents a substitute product recommended by the Substitution Agent.
/// </summary>
public sealed record ResolutionSubstitution
{
    /// <summary>Inventory SKU of the substitute product.</summary>
    public required string Sku { get; init; }

    /// <summary>Human-readable product name of the substitute.</summary>
    public required string ProductName { get; init; }
}

/// <summary>
/// Coupon or discount offer produced by the Promotion Agent.
/// </summary>
public sealed record ResolutionCoupon
{
    /// <summary>Redeemable coupon code.</summary>
    public required string Code { get; init; }

    /// <summary>Discount percentage (e.g., 25 for 25%).</summary>
    public required int DiscountPercent { get; init; }
}

/// <summary>
/// The structured result returned by the Order Resolution handoff sub-flow after
/// the OrderResolutionAgent, SubstitutionAgent, PromotionAgent, and/or EscalationAgent
/// have collaborated to determine the best outcome for an unfulfillable order.
/// </summary>
public sealed record OrderResolutionResult
{
    /// <summary>Unique identifier of the order being resolved.</summary>
    public required string OrderId { get; init; }

    /// <summary>Customer email address for communications.</summary>
    public required string CustomerEmail { get; init; }

    /// <summary>The final outcome determined by the resolution sub-flow.</summary>
    public required ResolutionOutcome Outcome { get; init; }

    /// <summary>Substitute products offered when the original items were unavailable.</summary>
    public IReadOnlyList<ResolutionSubstitution> SubstitutedItems { get; init; } = [];

    /// <summary>Coupon or promotional offer extended to the customer, if applicable.</summary>
    public ResolutionCoupon? Coupon { get; init; }

    /// <summary>Reason for escalation when <see cref="Outcome"/> is <see cref="ResolutionOutcome.Escalated"/>.</summary>
    public string? EscalationReason { get; init; }

    /// <summary>Additional context or explanation for the resolution decision.</summary>
    public string? Notes { get; init; }
}
