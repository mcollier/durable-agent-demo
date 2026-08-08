using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Services;

public class AzureBlobFeedbackStorageServiceTests
{
    private static FeedbackMessage CreateTestFeedback(
        string feedbackId = "fbk-10021",
        DateTimeOffset? submittedAt = null,
        ContactMethod preferredContactMethod = ContactMethod.Email) => new()
    {
        FeedbackId = feedbackId,
        SubmittedAt = submittedAt ?? new DateTimeOffset(2026, 8, 8, 14, 30, 0, TimeSpan.Zero),
        StoreId = "store-014",
        OrderId = "ord-77812",
        Customer = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = preferredContactMethod
        },
        Channel = "kiosk",
        Rating = 5,
        Comment = "Mint Condition is unreal. Best froyo I've had.",
        FlavorId = "flavor-001"
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
        string feedbackId = "fbk-10021",
        DateTimeOffset? submittedAt = null,
        ContactMethod preferredContactMethod = ContactMethod.Email) => new()
    {
        Feedback = CreateTestFeedback(feedbackId, submittedAt, preferredContactMethod),
        Analysis = CreateTestAnalysis(feedbackId),
        OrchestrationInstanceId = "instance-001"
    };

    private static (
        AzureBlobFeedbackStorageService Subject,
        BlobServiceClient BlobServiceClient,
        BlobContainerClient BlobContainerClient,
        BlobClient BlobClient) CreateSubject()
    {
        BlobServiceClient blobServiceClient = A.Fake<BlobServiceClient>();
        BlobContainerClient blobContainerClient = A.Fake<BlobContainerClient>();
        BlobClient blobClient = A.Fake<BlobClient>();

        A.CallTo(() => blobServiceClient.GetBlobContainerClient(A<string>._))
            .Returns(blobContainerClient);
        A.CallTo(() => blobContainerClient.GetBlobClient(A<string>._))
            .Returns(blobClient);
        A.CallTo(() => blobClient.UploadAsync(A<BinaryData>._, A<BlobUploadOptions>._, A<CancellationToken>._))
            .Returns(Task.FromResult(A.Fake<Response<BlobContentInfo>>()));

        AzureBlobFeedbackStorageService subject = new(
            blobServiceClient,
            A.Fake<ILogger<AzureBlobFeedbackStorageService>>());

        return (subject, blobServiceClient, blobContainerClient, blobClient);
    }

    [Fact]
    public async Task WhenPersistingValidRecord_ThenUsesCustomerFeedbackContainerBlobPathAndJsonContentType()
    {
        var record = CreateProcessedFeedbackRecord();
        var (subject, blobServiceClient, blobContainerClient, blobClient) = CreateSubject();

        var result = await subject.PersistAsync(record);

        var containerCall = Fake.GetCalls(blobServiceClient)
            .Single(call => call.Method.Name == nameof(BlobServiceClient.GetBlobContainerClient));
        var blobCall = Fake.GetCalls(blobContainerClient)
            .Single(call => call.Method.Name == nameof(BlobContainerClient.GetBlobClient));
        var uploadCall = Fake.GetCalls(blobClient)
            .Single(call => call.Method.Name == nameof(BlobClient.UploadAsync));

        Assert.Equal("customer-feedback", containerCall.GetArgument<string>(0));
        Assert.Equal("2026-08-08/fbk-10021.json", blobCall.GetArgument<string>(0));
        Assert.Equal("customer-feedback/2026-08-08/fbk-10021.json", result);

        BlobUploadOptions uploadOptions = Assert.IsType<BlobUploadOptions>(uploadCall.GetArgument<BlobUploadOptions>(1));
        BlobHttpHeaders headers = Assert.IsType<BlobHttpHeaders>(uploadOptions.HttpHeaders);
        Assert.Equal("application/json", headers.ContentType);
    }

    [Fact]
    public async Task WhenSubmittedAtCrossesUtcDayBoundary_ThenBlobPathUsesUtcDate()
    {
        var record = CreateProcessedFeedbackRecord(
            submittedAt: new DateTimeOffset(2026, 8, 8, 23, 30, 0, TimeSpan.FromHours(-7)));
        var (subject, _, blobContainerClient, _) = CreateSubject();

        var result = await subject.PersistAsync(record);

        var blobCall = Fake.GetCalls(blobContainerClient)
            .Single(call => call.Method.Name == nameof(BlobContainerClient.GetBlobClient));

        Assert.Equal("2026-08-09/fbk-10021.json", blobCall.GetArgument<string>(0));
        Assert.Equal("customer-feedback/2026-08-09/fbk-10021.json", result);
    }

    [Fact]
    public async Task WhenPersistingRecord_ThenSerializedPayloadUsesEnvelopeCamelCaseAndEnumStrings()
    {
        var record = CreateProcessedFeedbackRecord(preferredContactMethod: ContactMethod.Email);
        var (subject, _, _, blobClient) = CreateSubject();

        await subject.PersistAsync(record);

        var uploadCall = Fake.GetCalls(blobClient)
            .Single(call => call.Method.Name == nameof(BlobClient.UploadAsync));
        BinaryData payload = Assert.IsType<BinaryData>(uploadCall.GetArgument<BinaryData>(0));

        using JsonDocument document = JsonDocument.Parse(payload.ToMemory());

        Assert.True(document.RootElement.TryGetProperty("feedback", out JsonElement feedback));
        Assert.True(document.RootElement.TryGetProperty("analysis", out JsonElement analysis));
        Assert.False(document.RootElement.TryGetProperty("Feedback", out _));
        Assert.False(document.RootElement.TryGetProperty("Analysis", out _));

        Assert.Equal("fbk-10021", feedback.GetProperty("feedbackId").GetString());
        JsonElement preferredContactMethod = feedback.GetProperty("customer").GetProperty("preferredContactMethod");
        Assert.Equal("email", preferredContactMethod.GetString());
        Assert.Equal(JsonValueKind.String, preferredContactMethod.ValueKind);

        Assert.Equal("fbk-10021", analysis.GetProperty("feedbackId").GetString());
        Assert.Equal("positive", analysis.GetProperty("sentiment").GetString());
        Assert.True(analysis.GetProperty("risk").TryGetProperty("isHealthOrSafety", out _));
    }
}
