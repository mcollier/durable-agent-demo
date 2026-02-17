using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class GenerateCouponCodeToolTests
{
    [Fact]
    public void WhenCalledWithDefaults_ThenStartsWithFryoCouponPrefix()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode();

        Assert.StartsWith("FRYOCUPON-", result);
    }

    [Fact]
    public void WhenCalledWithDefaults_ThenContainsDefaultDiscountAndExpiration()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode();

        // Default: 10% discount, 30-day expiration
        Assert.EndsWith("-10PCT-30D", result);
    }

    [Fact]
    public void WhenCalledWithCustomValues_ThenContainsSpecifiedDiscountAndExpiration()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode(discountPercent: 25, expirationDays: 60);

        Assert.StartsWith("FRYOCUPON-", result);
        Assert.EndsWith("-25PCT-60D", result);
    }

    [Fact]
    public void WhenCalledTwice_ThenProducesUniqueCodes()
    {
        var code1 = GenerateCouponCodeTool.GenerateCouponCode();
        var code2 = GenerateCouponCodeTool.GenerateCouponCode();

        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public void WhenCalled_ThenGuidSegmentIsUppercase()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode();

        // Extract the 8-char GUID segment between "FRYOCUPON-" and "-10PCT-30D"
        var guidSegment = result["FRYOCUPON-".Length..result.IndexOf("-10PCT")];

        Assert.Equal(8, guidSegment.Length);
        Assert.Equal(guidSegment.ToUpper(), guidSegment);
    }
}
