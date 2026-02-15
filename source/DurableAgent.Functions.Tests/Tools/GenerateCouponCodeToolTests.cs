using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class GenerateCouponCodeToolTests
{
    [Fact]
    public void WhenCalled_ThenStartsWithFryoCouponPrefix()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode();

        Assert.StartsWith("FRYOCUPON-", result);
    }

    [Fact]
    public void WhenCalled_ThenHasCorrectLength()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode();

        // "FRYOCUPON-" (10 chars) + 8-char GUID segment = 18 chars
        Assert.Equal(18, result.Length);
    }

    [Fact]
    public void WhenCalledTwice_ThenProducesUniqueCodes()
    {
        var code1 = GenerateCouponCodeTool.GenerateCouponCode();
        var code2 = GenerateCouponCodeTool.GenerateCouponCode();

        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public void WhenCalled_ThenSuffixIsUppercase()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode();
        var suffix = result["FRYOCUPON-".Length..];

        Assert.Equal(suffix.ToUpper(), suffix);
    }
}
