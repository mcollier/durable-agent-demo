using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class OrderResolutionResultTests
{
    [Fact]
    public void WhenCreated_WithRequiredFields_ThenPropertiesAreSet()
    {
        var result = new OrderResolutionResult
        {
            OrderId = "ORD-001",
            CustomerEmail = "customer@example.com",
            Outcome = ResolutionOutcome.Promoted
        };

        Assert.Equal("ORD-001", result.OrderId);
        Assert.Equal("customer@example.com", result.CustomerEmail);
        Assert.Equal(ResolutionOutcome.Promoted, result.Outcome);
    }

    [Fact]
    public void WhenSubstitutedItems_NotProvided_ThenDefaultsToEmptyList()
    {
        var result = new OrderResolutionResult
        {
            OrderId = "ORD-002",
            CustomerEmail = "customer@example.com",
            Outcome = ResolutionOutcome.Substituted
        };

        Assert.NotNull(result.SubstitutedItems);
        Assert.Empty(result.SubstitutedItems);
    }

    [Fact]
    public void WhenOutcomeIsEscalated_ThenEscalationReasonCanBeSet()
    {
        var result = new OrderResolutionResult
        {
            OrderId = "ORD-003",
            CustomerEmail = "customer@example.com",
            Outcome = ResolutionOutcome.Escalated,
            EscalationReason = "Order value exceeds autonomous approval threshold."
        };

        Assert.Equal(ResolutionOutcome.Escalated, result.Outcome);
        Assert.Equal("Order value exceeds autonomous approval threshold.", result.EscalationReason);
    }

    [Fact]
    public void WhenCouponProvided_ThenCouponIsSet()
    {
        var coupon = new ResolutionCoupon { Code = "FRYOCUPON-ABCD1234-25PCT-30D", DiscountPercent = 25 };
        var result = new OrderResolutionResult
        {
            OrderId = "ORD-004",
            CustomerEmail = "customer@example.com",
            Outcome = ResolutionOutcome.Promoted,
            Coupon = coupon
        };

        Assert.NotNull(result.Coupon);
        Assert.Equal("FRYOCUPON-ABCD1234-25PCT-30D", result.Coupon.Code);
        Assert.Equal(25, result.Coupon.DiscountPercent);
    }

    [Fact]
    public void WhenSubstitutedItemsProvided_ThenListIsSet()
    {
        var items = new List<ResolutionSubstitution>
        {
            new() { Sku = "STR-TUB", ProductName = "Strawberry" },
            new() { Sku = "MNG-TUB", ProductName = "Mango" }
        };

        var result = new OrderResolutionResult
        {
            OrderId = "ORD-005",
            CustomerEmail = "customer@example.com",
            Outcome = ResolutionOutcome.Substituted,
            SubstitutedItems = items
        };

        Assert.Equal(2, result.SubstitutedItems.Count);
        Assert.Equal("STR-TUB", result.SubstitutedItems[0].Sku);
    }

    [Fact]
    public void WhenUnresolvable_ThenNotesCanBeSet()
    {
        var result = new OrderResolutionResult
        {
            OrderId = "ORD-006",
            CustomerEmail = "customer@example.com",
            Outcome = ResolutionOutcome.Unresolvable,
            Notes = "All substitutions exhausted; no promotional options applicable."
        };

        Assert.Equal(ResolutionOutcome.Unresolvable, result.Outcome);
        Assert.Equal("All substitutions exhausted; no promotional options applicable.", result.Notes);
    }

    [Theory]
    [InlineData(ResolutionOutcome.Substituted)]
    [InlineData(ResolutionOutcome.Promoted)]
    [InlineData(ResolutionOutcome.Escalated)]
    [InlineData(ResolutionOutcome.Unresolvable)]
    public void WhenOutcomeIsSet_ThenAllEnumValuesAreValid(ResolutionOutcome outcome)
    {
        var result = new OrderResolutionResult
        {
            OrderId = "ORD-007",
            CustomerEmail = "customer@example.com",
            Outcome = outcome
        };

        Assert.Equal(outcome, result.Outcome);
    }
}
