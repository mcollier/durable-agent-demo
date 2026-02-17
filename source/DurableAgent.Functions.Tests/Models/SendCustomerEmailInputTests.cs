using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Tests.Models;

public class SendCustomerEmailInputTests
{
    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var input = CreateTestInput();

        Assert.Equal("fbk-10021", input.FeedbackId);
        Assert.Equal("CASE-500", input.CaseId);
        Assert.Equal("Aidan", input.RecipientName);
        Assert.Equal("aidan@example.com", input.RecipientEmail);
        Assert.Equal("Thank you for your feedback!", input.Body);
    }

    [Fact]
    public void WhenTwoInputsHaveSameValues_ThenTheyAreEqual()
    {
        var a = CreateTestInput();
        var b = CreateTestInput();

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoInputsHaveDifferentFeedbackId_ThenTheyAreNotEqual()
    {
        var a = CreateTestInput();
        var b = CreateTestInput() with { FeedbackId = "fbk-999" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenCopiedWithNewCaseId_ThenOnlyCaseIdChanges()
    {
        var original = CreateTestInput();
        var copy = original with { CaseId = "CASE-999" };

        Assert.Equal("CASE-999", copy.CaseId);
        Assert.Equal("fbk-10021", copy.FeedbackId);
        Assert.Equal("Aidan", copy.RecipientName);
        Assert.Equal("aidan@example.com", copy.RecipientEmail);
        Assert.Equal("Thank you for your feedback!", copy.Body);
    }

    private static SendCustomerEmailInput CreateTestInput() => new()
    {
        FeedbackId = "fbk-10021",
        CaseId = "CASE-500",
        RecipientName = "Aidan",
        RecipientEmail = "aidan@example.com",
        Body = "Thank you for your feedback!"
    };
}
