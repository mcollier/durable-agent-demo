using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class FeedbackResultTests
{
    [Fact]
    public void WhenCreatedWithAllProperties_ThenPropertiesAreSet()
    {
        var result = CreateTestResult();

        Assert.Equal("fbk-001", result.FeedbackId);
        Assert.Equal("positive", result.Sentiment);
        Assert.Equal("THANK_YOU", result.Action);
        Assert.Equal(0.95, result.Confidence);
        Assert.NotNull(result.Risk);
        Assert.NotNull(result.Message);
        Assert.NotNull(result.FollowUp);
    }

    [Fact]
    public void WhenCouponIsNull_ThenCouponPropertyIsNull()
    {
        var result = CreateTestResult(includeCoupon: false);

        Assert.Null(result.Coupon);
    }

    [Fact]
    public void WhenCouponIsProvided_ThenCouponPropertiesAreSet()
    {
        var result = CreateTestResult(includeCoupon: true);

        Assert.NotNull(result.Coupon);
        Assert.Equal("FRYOCUPON-ABC123", result.Coupon.Code);
        Assert.Equal(25, result.Coupon.DiscountPercent);
    }

    [Fact]
    public void WhenTwoResultsShareSameCollectionInstance_ThenTheyAreEqual()
    {
        // Record equality uses reference equality for IReadOnlyList<string>,
        // so both must share the same Keywords instance.
        IReadOnlyList<string> sharedKeywords = ["great", "love"];
        var a = CreateTestResult() with { Risk = new RiskAssessment { IsHealthOrSafety = false, IsFoodQualityIssue = false, Keywords = sharedKeywords } };
        var b = CreateTestResult() with { Risk = new RiskAssessment { IsHealthOrSafety = false, IsFoodQualityIssue = false, Keywords = sharedKeywords } };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoResultsHaveDifferentIds_ThenTheyAreNotEqual()
    {
        var a = CreateTestResult();
        var b = CreateTestResult() with { FeedbackId = "fbk-999" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenRiskAssessmentCreated_ThenPropertiesAreSet()
    {
        var risk = new RiskAssessment
        {
            IsHealthOrSafety = true,
            IsFoodQualityIssue = false,
            Keywords = ["found", "hair"]
        };

        Assert.True(risk.IsHealthOrSafety);
        Assert.False(risk.IsFoodQualityIssue);
        Assert.Equal(2, risk.Keywords.Count);
        Assert.Contains("found", risk.Keywords);
    }

    [Fact]
    public void WhenRiskAssessmentKeywordsOmitted_ThenDefaultsToEmptyList()
    {
        var risk = new RiskAssessment
        {
            IsHealthOrSafety = false,
            IsFoodQualityIssue = false
        };

        Assert.Empty(risk.Keywords);
    }

    [Fact]
    public void WhenResponseMessageCreated_ThenPropertiesAreSet()
    {
        var msg = new ResponseMessage
        {
            Subject = "Thanks!",
            Body = "We appreciate your feedback."
        };

        Assert.Equal("Thanks!", msg.Subject);
        Assert.Equal("We appreciate your feedback.", msg.Body);
    }

    [Fact]
    public void WhenCouponDetailsCreated_ThenPropertiesAreSet()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(30);
        var coupon = new CouponDetails
        {
            Code = "FRYOCUPON-XYZ",
            DiscountPercent = 15,
            ExpiresAt = expires
        };

        Assert.Equal("FRYOCUPON-XYZ", coupon.Code);
        Assert.Equal(15, coupon.DiscountPercent);
        Assert.Equal(expires, coupon.ExpiresAt);
    }

    [Fact]
    public void WhenFollowUpRequiresHuman_ThenCaseIdIsSet()
    {
        var followUp = new FollowUp
        {
            RequiresHuman = true,
            CaseId = "CASE-123"
        };

        Assert.True(followUp.RequiresHuman);
        Assert.Equal("CASE-123", followUp.CaseId);
    }

    [Fact]
    public void WhenFollowUpDoesNotRequireHuman_ThenCaseIdIsNull()
    {
        var followUp = new FollowUp
        {
            RequiresHuman = false
        };

        Assert.False(followUp.RequiresHuman);
        Assert.Null(followUp.CaseId);
    }

    [Fact]
    public void WhenTwoFollowUpsHaveSameValues_ThenTheyAreEqual()
    {
        var a = new FollowUp { RequiresHuman = true, CaseId = "CASE-1" };
        var b = new FollowUp { RequiresHuman = true, CaseId = "CASE-1" };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoResponseMessagesHaveSameValues_ThenTheyAreEqual()
    {
        var a = new ResponseMessage { Subject = "Hi", Body = "Hello" };
        var b = new ResponseMessage { Subject = "Hi", Body = "Hello" };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoCouponDetailsHaveSameValues_ThenTheyAreEqual()
    {
        var expires = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var a = new CouponDetails { Code = "ABC", DiscountPercent = 10, ExpiresAt = expires };
        var b = new CouponDetails { Code = "ABC", DiscountPercent = 10, ExpiresAt = expires };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoRiskAssessmentsShareKeywords_ThenTheyAreEqual()
    {
        IReadOnlyList<string> keywords = ["hair"];
        var a = new RiskAssessment { IsHealthOrSafety = true, IsFoodQualityIssue = false, Keywords = keywords };
        var b = new RiskAssessment { IsHealthOrSafety = true, IsFoodQualityIssue = false, Keywords = keywords };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenRiskAssessmentCopiedWithDifferentFlag_ThenOnlyFlagChanges()
    {
        var original = new RiskAssessment { IsHealthOrSafety = false, IsFoodQualityIssue = false };
        var copy = original with { IsHealthOrSafety = true };

        Assert.True(copy.IsHealthOrSafety);
        Assert.False(copy.IsFoodQualityIssue);
    }

    [Fact]
    public void WhenResponseMessageCopiedWithNewSubject_ThenOnlySubjectChanges()
    {
        var original = new ResponseMessage { Subject = "Old", Body = "Body" };
        var copy = original with { Subject = "New" };

        Assert.Equal("New", copy.Subject);
        Assert.Equal("Body", copy.Body);
    }

    [Fact]
    public void WhenCouponDetailsCopiedWithNewCode_ThenOnlyCodeChanges()
    {
        var expires = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var original = new CouponDetails { Code = "OLD", DiscountPercent = 10, ExpiresAt = expires };
        var copy = original with { Code = "NEW" };

        Assert.Equal("NEW", copy.Code);
        Assert.Equal(10, copy.DiscountPercent);
    }

    [Fact]
    public void WhenFollowUpCopiedWithCaseId_ThenCaseIdIsSet()
    {
        var original = new FollowUp { RequiresHuman = false };
        var copy = original with { RequiresHuman = true, CaseId = "CASE-NEW" };

        Assert.True(copy.RequiresHuman);
        Assert.Equal("CASE-NEW", copy.CaseId);
    }

    private static FeedbackResult CreateTestResult(bool includeCoupon = false) => new()
    {
        FeedbackId = "fbk-001",
        Sentiment = "positive",
        Risk = new RiskAssessment
        {
            IsHealthOrSafety = false,
            IsFoodQualityIssue = false,
            Keywords = ["great", "love"]
        },
        Action = "THANK_YOU",
        Message = new ResponseMessage
        {
            Subject = "Thanks for the feedback!",
            Body = "We appreciate your kind words."
        },
        Coupon = includeCoupon
            ? new CouponDetails
            {
                Code = "FRYOCUPON-ABC123",
                DiscountPercent = 25,
                ExpiresAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)
            }
            : null,
        FollowUp = new FollowUp
        {
            RequiresHuman = false
        },
        Confidence = 0.95
    };
}
