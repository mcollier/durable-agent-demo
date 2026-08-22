using System.ComponentModel;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Generates unique coupon codes for the AI agent.
/// </summary>
public static class GenerateCouponCodeTool
{
    private static readonly HashSet<int> ApprovedDiscountPercentages = [10, 15, 20, 25];

    [Description("Generates a unique coupon code using an approved discount tier. Call this tool exactly once when a valid order has an inventory shortfall; never fabricate a code.")]
    public static string GenerateCouponCode(
        [Description("An approved discount percentage: 10, 15, 20, or 25.")] int discountPercent = 10,
        [Description("The number of days from today until the coupon expires.")] int expirationDays = 30)
    {
        if (!ApprovedDiscountPercentages.Contains(discountPercent))
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountPercent),
                discountPercent,
                "Discount percentage must be 10, 15, 20, or 25.");
        }

        // In a real implementation, this would call a coupon service to generate a unique code and store the details. For this example, we'll return a placeholder value.
        return $"FRYOCUPON-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}-{discountPercent}PCT-{expirationDays}D";
    }
}
