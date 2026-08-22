using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Tests.Models;

public class OrderFulfillmentResultTests
{
    [Fact]
    public void WhenOrderIsInvalid_ThenValidationFailureCanFlowWithoutFulfillmentData()
    {
        OrderFulfillmentResult result = new()
        {
            IsValidOrder = false,
            ValidationError = "Missing customer email.",
            CanFullyFulfill = false
        };

        Assert.False(result.IsValidOrder);
        Assert.Equal("Missing customer email.", result.ValidationError);
        Assert.Null(result.OrderId);
        Assert.Null(result.CustomerEmail);
        Assert.Empty(result.Items);
        Assert.Empty(result.AlternativeRecommendations);
        Assert.Null(result.Coupon);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(25)]
    public void WhenOrderHasShortfall_ThenApprovedCouponTierCanBeRepresented(int discountPercent)
    {
        OrderFulfillmentResult result = new()
        {
            IsValidOrder = true,
            OrderId = "order-1",
            CustomerEmail = "customer@example.com",
            CanFullyFulfill = false,
            Coupon = new Coupon
            {
                Code = $"FRYOCUPON-TEST-{discountPercent}PCT-30D",
                DiscountPercent = discountPercent
            },
            AlternativeRecommendations =
            [
                new AlternativeRecommendation
                {
                    Sku = "STR-TUB",
                    ProductName = "Strawberry"
                }
            ]
        };

        Assert.Equal(discountPercent, result.Coupon.DiscountPercent);
        Assert.Single(result.AlternativeRecommendations);
    }
}
