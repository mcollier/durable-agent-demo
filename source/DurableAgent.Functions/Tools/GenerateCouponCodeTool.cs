using System.ComponentModel;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Generates unique coupon codes for the AI agent.
/// </summary>
public static class GenerateCouponCodeTool
{
    [Description("Generates a unique coupon code with a specific format. ")]
    public static string GenerateCouponCode()
    {
        // In a real implementation, this would call a coupon service to generate a unique code and store the details. For this example, we'll return a placeholder value.
        return $"FRYOCUPON-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
    }
}
