using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class FeedbackMessageTests
{
    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var message = new FeedbackMessage
        {
            Id = "fb-001",
            Content = "Great service!"
        };

        Assert.Equal("fb-001", message.Id);
        Assert.Equal("Great service!", message.Content);
    }

    [Fact]
    public void WhenTimestampNotProvided_ThenDefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var message = new FeedbackMessage
        {
            Id = "fb-002",
            Content = "Test"
        };

        var after = DateTimeOffset.UtcNow;

        Assert.InRange(message.Timestamp, before, after);
    }

    [Fact]
    public void WhenTwoMessagesHaveSameValues_ThenTheyAreEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var a = new FeedbackMessage { Id = "fb-003", Content = "Same", Timestamp = timestamp };
        var b = new FeedbackMessage { Id = "fb-003", Content = "Same", Timestamp = timestamp };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoMessagesHaveDifferentIds_ThenTheyAreNotEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var a = new FeedbackMessage { Id = "fb-004", Content = "Same", Timestamp = timestamp };
        var b = new FeedbackMessage { Id = "fb-005", Content = "Same", Timestamp = timestamp };

        Assert.NotEqual(a, b);
    }
}
