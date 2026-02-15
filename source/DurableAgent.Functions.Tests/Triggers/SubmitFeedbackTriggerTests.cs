using System.Net;
using System.Text;
using System.Text.Json;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Services;
using DurableAgent.Functions.Tests.TestHelpers;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class SubmitFeedbackTriggerTests
{
    private readonly IFeedbackQueueSender _queueSender = A.Fake<IFeedbackQueueSender>();
    private readonly ILogger<SubmitFeedbackTrigger> _logger = A.Fake<ILogger<SubmitFeedbackTrigger>>();
    private readonly FunctionContext _functionContext = A.Fake<FunctionContext>();

    private FakeHttpRequestData CreateRequest(object? body = null)
    {
        var json = body is not null
            ? JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : string.Empty;

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new FakeHttpRequestData(_functionContext, body: stream);
    }

    private static object CreateValidRequestBody(string? feedbackId = null) => new
    {
        feedbackId,
        storeId = "store-001",
        orderId = "ord-100",
        customer = new
        {
            preferredName = "Aidan",
            firstName = "Aidan",
            lastName = "Smith",
            email = "aidan@example.com",
            phoneNumber = "555-0100",
            preferredContactMethod = "Email"
        },
        channel = "web",
        rating = 4,
        comment = "Great froyo!"
    };

    // ── Success ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenValidPayload_ThenReturns200AndEnqueues()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var request = CreateRequest(CreateValidRequestBody());

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        A.CallTo(() => _queueSender.SendAsync(
                A<FeedbackMessage>.That.Matches(m =>
                    m.StoreId == "store-001" &&
                    m.Rating == 4 &&
                    !string.IsNullOrWhiteSpace(m.FeedbackId)),
                A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenFeedbackIdProvided_ThenUsesProvidedId()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var request = CreateRequest(CreateValidRequestBody(feedbackId: "my-custom-id"));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        A.CallTo(() => _queueSender.SendAsync(
                A<FeedbackMessage>.That.Matches(m => m.FeedbackId == "my-custom-id"),
                A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenFeedbackIdOmitted_ThenGeneratesGuid()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var request = CreateRequest(CreateValidRequestBody());

        await trigger.RunAsync(request, CancellationToken.None);

        A.CallTo(() => _queueSender.SendAsync(
                A<FeedbackMessage>.That.Matches(m => IsValidGuid(m.FeedbackId)),
                A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    // ── Validation errors → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task WhenInvalidJson_ThenReturns400()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-json!!!"));
        var request = new FakeHttpRequestData(_functionContext, body: stream);

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task WhenEmptyBody_ThenReturns400()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        var request = new FakeHttpRequestData(_functionContext, body: stream);

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task WhenRequiredFieldsMissing_ThenReturns400WithErrors()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        // Missing storeId, orderId, customer, channel, rating, comment
        var request = CreateRequest(new { feedbackId = "test" });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var fakeResponse = (FakeHttpResponseData)response;
        var body = fakeResponse.ReadBodyAsString();
        Assert.Contains("storeId is required", body);
        Assert.Contains("orderId is required", body);
        Assert.Contains("customer is required", body);
        Assert.Contains("channel is required", body);
        Assert.Contains("rating is required", body);
        Assert.Contains("comment is required", body);

        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(100)]
    public async Task WhenRatingOutOfRange_ThenReturns400(int rating)
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var payload = new
        {
            storeId = "store-001",
            orderId = "ord-1",
            customer = new
            {
                preferredName = "X",
                firstName = "X",
                lastName = "X",
                email = "x@test.com",
                phoneNumber = "555",
                preferredContactMethod = "Email"
            },
            channel = "web",
            rating,
            comment = "Test"
        };
        var request = CreateRequest(payload);

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    // ── Service Bus errors ──────────────────────────────────────────────────

    [Fact]
    public async Task WhenTransientServiceBusError_ThenReturns503()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var request = CreateRequest(CreateValidRequestBody());

        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .Throws(new ServiceBusException("transient", ServiceBusFailureReason.ServiceBusy));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task WhenNonTransientServiceBusError_ThenReturns500()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var request = CreateRequest(CreateValidRequestBody());

        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .Throws(new ServiceBusException("not transient", ServiceBusFailureReason.MessageSizeExceeded));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task WhenUnexpectedError_ThenReturns500()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);
        var request = CreateRequest(CreateValidRequestBody());

        A.CallTo(() => _queueSender.SendAsync(A<FeedbackMessage>.Ignored, A<CancellationToken>.Ignored))
            .Throws(new InvalidOperationException("boom"));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // ── Guard clauses ───────────────────────────────────────────────────────

    [Fact]
    public async Task WhenRequestIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new SubmitFeedbackTrigger(_queueSender, _logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }

    private static bool IsValidGuid(string value) => Guid.TryParse(value, out _);
}
