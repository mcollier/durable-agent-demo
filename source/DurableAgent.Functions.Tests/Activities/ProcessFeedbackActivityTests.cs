using Azure;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Activities;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Services;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Tests.Activities;

public class ProcessFeedbackActivityTests
{
    private const string ExpectedBlobPath = "customer-feedback/2026-08-08/fbk-10021.json";

    private static FeedbackMessage CreateTestFeedback(
        int rating = 5,
        string feedbackId = "fbk-10021") => new()
    {
        FeedbackId = feedbackId,
        SubmittedAt = new DateTimeOffset(2026, 8, 8, 14, 30, 0, TimeSpan.Zero),
        StoreId = "store-014",
        OrderId = "ord-77812",
        Customer = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        },
        Channel = "kiosk",
        Rating = rating,
        Comment = "Mint Condition is unreal. Best froyo I've had."
    };

    private static FeedbackResult CreateTestAnalysis(string feedbackId = "fbk-10021") => new()
    {
        FeedbackId = feedbackId,
        Sentiment = "positive",
        Risk = new RiskAssessment
        {
            IsHealthOrSafety = false,
            IsFoodQualityIssue = false,
            Keywords = []
        },
        Action = "THANK_YOU",
        Coupon = null,
        FollowUp = new FollowUp
        {
            RequiresHuman = false,
            CaseId = null
        },
        Confidence = 0.98
    };

    private static ProcessedFeedbackRecord CreateProcessedFeedbackRecord(
        int rating = 5,
        string feedbackId = "fbk-10021",
        string? analysisFeedbackId = null) => new()
    {
        Feedback = CreateTestFeedback(rating, feedbackId),
        Analysis = CreateTestAnalysis(analysisFeedbackId ?? feedbackId),
        OrchestrationInstanceId = "instance-001"
    };

    private static FunctionContext CreateFakeFunctionContext(IFeedbackBlobStorageService? feedbackBlobStorageService = null)
    {
        feedbackBlobStorageService ??= A.Fake<IFeedbackBlobStorageService>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(feedbackBlobStorageService);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var context = A.Fake<FunctionContext>();
        A.CallTo(() => context.InstanceServices).Returns(serviceProvider);

        return context;
    }

    [Fact]
    public async Task WhenValidInput_ThenCallsStorageServiceOnceAndReturnsBlobPath()
    {
        var input = CreateProcessedFeedbackRecord();
        var storageService = A.Fake<IFeedbackBlobStorageService>();
        A.CallTo(() => storageService.PersistAsync(input, A<CancellationToken>._))
            .Returns(ExpectedBlobPath);
        var context = CreateFakeFunctionContext(storageService);

        var result = await ProcessFeedbackActivity.RunAsync(input, context);

        Assert.Equal($"Persisted feedback 'fbk-10021' to '{ExpectedBlobPath}'", result);
        A.CallTo(() => storageService.PersistAsync(input, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenInputIsNull_ThenThrowsArgumentNullException()
    {
        var context = CreateFakeFunctionContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ProcessFeedbackActivity.RunAsync(null!, context));
    }

    [Fact]
    public async Task WhenFeedbackIsNull_ThenThrowsArgumentNullException()
    {
        var input = CreateProcessedFeedbackRecord() with { Feedback = null! };
        var context = CreateFakeFunctionContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));
    }

    [Fact]
    public async Task WhenAnalysisIsNull_ThenThrowsArgumentNullException()
    {
        var input = CreateProcessedFeedbackRecord() with { Analysis = null! };
        var context = CreateFakeFunctionContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task WhenRatingBelowMinimum_ThenThrowsArgumentOutOfRangeException(int rating)
    {
        var input = CreateProcessedFeedbackRecord(rating: rating);
        var context = CreateFakeFunctionContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(100)]
    public async Task WhenRatingAboveMaximum_ThenThrowsArgumentOutOfRangeException(int rating)
    {
        var input = CreateProcessedFeedbackRecord(rating: rating);
        var context = CreateFakeFunctionContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));
    }

    [Fact]
    public async Task WhenFeedbackAndAnalysisIdsDoNotMatch_ThenThrowsInvalidOperationException()
    {
        var input = CreateProcessedFeedbackRecord(analysisFeedbackId: "fbk-99999");
        var context = CreateFakeFunctionContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));

        Assert.Equal("Feedback.FeedbackId must exactly match Analysis.FeedbackId.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fbk/10021")]
    public async Task WhenFeedbackIdIsInvalidOrUnsafe_ThenThrowsInvalidOperationException(string feedbackId)
    {
        var input = CreateProcessedFeedbackRecord(feedbackId: feedbackId);
        var storageService = A.Fake<IFeedbackBlobStorageService>();
        A.CallTo(() => storageService.PersistAsync(input, A<CancellationToken>._))
            .Throws(new InvalidOperationException(
                string.IsNullOrWhiteSpace(feedbackId)
                    ? "Feedback.FeedbackId is required."
                    : "Feedback.FeedbackId cannot contain path separators."));
        var context = CreateFakeFunctionContext(storageService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));

        Assert.Contains("Feedback.FeedbackId", exception.Message);
    }

    [Fact]
    public async Task WhenStorageUploadFails_ThenExceptionPropagates()
    {
        var input = CreateProcessedFeedbackRecord();
        var storageException = new RequestFailedException(503, "Simulated upload failure");
        var storageService = A.Fake<IFeedbackBlobStorageService>();
        A.CallTo(() => storageService.PersistAsync(input, A<CancellationToken>._))
            .Throws(storageException);
        var context = CreateFakeFunctionContext(storageService);

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            ProcessFeedbackActivity.RunAsync(input, context));

        Assert.Same(storageException, exception);
    }

    [Fact]
    public void WhenLegacyRunReceivesValidInput_ThenReturnsProcessedResult()
    {
        var feedback = CreateTestFeedback();
        var context = CreateFakeFunctionContext();

        var result = ProcessFeedbackActivity.Run(feedback, context);

        Assert.Contains("fbk-10021", result);
        Assert.StartsWith("Processed feedback", result);
    }
}
