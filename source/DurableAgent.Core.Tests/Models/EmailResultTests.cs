using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class EmailResultTests
{
    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var result = CreateTestEmailResult();

        Assert.Equal("Aidan", result.RecipientName);
        Assert.Equal("aidan@example.com", result.RecipientEmail);
        Assert.Equal("Thank you for your feedback!", result.Body);
    }

    [Fact]
    public void WhenTwoEmailResultsHaveSameValues_ThenTheyAreEqual()
    {
        var a = CreateTestEmailResult();
        var b = CreateTestEmailResult();

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoEmailResultsHaveDifferentRecipientName_ThenTheyAreNotEqual()
    {
        var a = CreateTestEmailResult();
        var b = CreateTestEmailResult() with { RecipientName = "Jordan" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenTwoEmailResultsHaveDifferentEmail_ThenTheyAreNotEqual()
    {
        var a = CreateTestEmailResult();
        var b = CreateTestEmailResult() with { RecipientEmail = "other@example.com" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenTwoEmailResultsHaveDifferentBody_ThenTheyAreNotEqual()
    {
        var a = CreateTestEmailResult();
        var b = CreateTestEmailResult() with { Body = "Different body text." };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenCopiedWithNewBody_ThenOnlyBodyChanges()
    {
        var original = CreateTestEmailResult();
        var copy = original with { Body = "Updated body." };

        Assert.Equal("Aidan", copy.RecipientName);
        Assert.Equal("aidan@example.com", copy.RecipientEmail);
        Assert.Equal("Updated body.", copy.Body);
    }

    private static EmailResult CreateTestEmailResult() => new()
    {
        RecipientName = "Aidan",
        RecipientEmail = "aidan@example.com",
        Body = "Thank you for your feedback!"
    };
}
