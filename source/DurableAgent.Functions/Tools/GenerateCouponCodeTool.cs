using System.ComponentModel;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Generates unique coupon codes for the AI agent.
/// </summary>
public static class GenerateCouponCodeTool
{
    [Description("REQUIRED when action is ISSUE_COUPON. Generates a unique coupon code for the customer. Returns the coupon code string. The agent MUST call this tool to obtain a coupon code — never fabricate one.")]
    public static string GenerateCouponCode(
        [Description("The discount percentage for the coupon (e.g. 10 for 10% off).")] int discountPercent = 10,
        [Description("The number of days from today until the coupon expires.")] int expirationDays = 30)
    {
        // In a real implementation, this would call a coupon service to generate a unique code and store the details. For this example, we'll return a placeholder value.
        return $"FRYOCUPON-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}-{discountPercent}PCT-{expirationDays}D";
    }
}
