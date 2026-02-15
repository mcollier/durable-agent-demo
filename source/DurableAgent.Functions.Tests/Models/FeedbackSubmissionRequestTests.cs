using DurableAgent.Core.Models;
using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Tests.Models;

public class FeedbackSubmissionRequestTests
{
    private static FeedbackSubmissionRequest CreateValid() => new()
    {
        StoreId = "store-001",
        OrderId = "ord-100",
        Customer = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        },
        Channel = "web",
        Rating = 4,
        Comment = "Great froyo!"
    };

    [Fact]
    public void WhenAllFieldsValid_ThenNoErrors()
    {
        var request = CreateValid();
        Assert.Empty(request.Validate());
    }

    [Fact]
    public void WhenFeedbackIdOmitted_ThenToFeedbackMessageGeneratesGuid()
    {
        var request = CreateValid();
        var message = request.ToFeedbackMessage();
        Assert.True(Guid.TryParse(message.FeedbackId, out _));
    }

    [Fact]
    public void WhenFeedbackIdProvided_ThenToFeedbackMessageUsesIt()
    {
        var request = CreateValid() with { FeedbackId = "custom-id" };
        var message = request.ToFeedbackMessage();
        Assert.Equal("custom-id", message.FeedbackId);
    }

    [Fact]
    public void WhenSubmittedAtOmitted_ThenToFeedbackMessageDefaultsToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var message = CreateValid().ToFeedbackMessage();
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(message.SubmittedAt, before, after);
    }

    [Fact]
    public void WhenSubmittedAtProvided_ThenToFeedbackMessageUsesIt()
    {
        var ts = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var request = CreateValid() with { SubmittedAt = ts };
        var message = request.ToFeedbackMessage();
        Assert.Equal(ts, message.SubmittedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public void WhenRatingOutOfRange_ThenValidationError(int rating)
    {
        var request = CreateValid() with { Rating = rating };
        var errors = request.Validate();
        Assert.Contains(errors, e => e.Contains("rating must be between 1 and 5"));
    }

    [Fact]
    public void WhenRatingNull_ThenValidationError()
    {
        var request = CreateValid() with { Rating = null };
        var errors = request.Validate();
        Assert.Contains(errors, e => e.Contains("rating is required"));
    }

    [Fact]
    public void WhenCustomerNull_ThenValidationError()
    {
        var request = CreateValid() with { Customer = null };
        var errors = request.Validate();
        Assert.Contains(errors, e => e.Contains("customer is required"));
    }

    [Fact]
    public void WhenMultipleFieldsMissing_ThenAllErrorsReported()
    {
        var request = new FeedbackSubmissionRequest();
        var errors = request.Validate();

        Assert.Contains(errors, e => e.Contains("storeId is required"));
        Assert.Contains(errors, e => e.Contains("orderId is required"));
        Assert.Contains(errors, e => e.Contains("customer is required"));
        Assert.Contains(errors, e => e.Contains("channel is required"));
        Assert.Contains(errors, e => e.Contains("rating is required"));
        Assert.Contains(errors, e => e.Contains("comment is required"));
    }
}
