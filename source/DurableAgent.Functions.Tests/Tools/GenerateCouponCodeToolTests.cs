using System.Text.Json;

using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class GenerateCouponCodeToolTests
{
    [Fact]
    public void WhenCalled_ThenReturnsValidJson()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode(10, 30);

        var doc = JsonDocument.Parse(result);
        Assert.NotNull(doc.RootElement.GetProperty("code").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("discountPercent").GetInt32());
        Assert.NotNull(doc.RootElement.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public void WhenCalled_ThenCodeStartsWithFryoCouponPrefix()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode(10, 30);

        var doc = JsonDocument.Parse(result);
        var code = doc.RootElement.GetProperty("code").GetString()!;
        Assert.StartsWith("FRYOCUPON-", code);
    }

    [Fact]
    public void WhenCalledWithCustomValues_ThenReturnsSpecifiedDiscount()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode(discountPercent: 25, expirationDays: 60);

        var doc = JsonDocument.Parse(result);
        Assert.Equal(25, doc.RootElement.GetProperty("discountPercent").GetInt32());
    }

    [Fact]
    public void WhenCalledTwice_ThenProducesUniqueCodes()
    {
        var result1 = GenerateCouponCodeTool.GenerateCouponCode(10, 30);
        var result2 = GenerateCouponCodeTool.GenerateCouponCode(10, 30);

        var code1 = JsonDocument.Parse(result1).RootElement.GetProperty("code").GetString();
        var code2 = JsonDocument.Parse(result2).RootElement.GetProperty("code").GetString();

        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public void WhenCalled_ThenExpiresAtIsFutureDate()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode(10, 30);

        var doc = JsonDocument.Parse(result);
        var expiresAt = DateTimeOffset.Parse(doc.RootElement.GetProperty("expiresAt").GetString()!);

        Assert.True(expiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void WhenCalled_ThenGuidSegmentIsUppercase()
    {
        var result = GenerateCouponCodeTool.GenerateCouponCode(10, 30);

        var doc = JsonDocument.Parse(result);
        var code = doc.RootElement.GetProperty("code").GetString()!;

        // Extract the 8-char GUID segment after "FRYOCUPON-"
        var guidSegment = code["FRYOCUPON-".Length..];

        Assert.Equal(8, guidSegment.Length);
        Assert.Equal(guidSegment.ToUpper(), guidSegment);
    }
}
