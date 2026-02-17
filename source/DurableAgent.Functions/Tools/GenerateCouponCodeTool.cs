using System.ComponentModel;
using System.Text.Json;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Generates unique coupon codes for the AI agent.
/// </summary>
public static class GenerateCouponCodeTool
{
    [Description("Generates a unique coupon code and returns a JSON object with the code, discountPercent, and expiresAt (ISO 8601 UTC). Call this tool to obtain coupon details for the response.")]
    public static string GenerateCouponCode(
        [Description("The discount percentage for the coupon, e.g. 10 for 10% off.")] int discountPercent,
        [Description("The number of days from today until the coupon expires.")] int expirationDays)
    {
        // In a real implementation, this would call a coupon service to generate a unique code and store the details. For this example, we'll return a placeholder value.
        var code = $"FRYOCUPON-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);

        return JsonSerializer.Serialize(new
        {
            code,
            discountPercent,
            expiresAt = expiresAt.ToString("o")
        });
    }
}
