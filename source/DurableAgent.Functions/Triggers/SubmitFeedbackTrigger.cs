using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// HTTP POST endpoint that accepts feedback submissions and enqueues them
/// to the <c>inbound-feedback</c> Service Bus queue for downstream processing.
/// </summary>
public sealed class SubmitFeedbackTrigger(
    IFeedbackQueueSender queueSender,
    ILogger<SubmitFeedbackTrigger> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Function(nameof(SubmitFeedbackTrigger))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "feedback")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── Deserialize request body ────────────────────────────────────────
        FeedbackSubmissionRequest? submission;
        try
        {
            submission = await JsonSerializer.DeserializeAsync<FeedbackSubmissionRequest>(
                request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON in request body.");
            return await CreateErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "Request body contains invalid JSON.");
        }

        if (submission is null)
        {
            return await CreateErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "Request body is empty or null.");
        }

        // ── Validate ────────────────────────────────────────────────────────
        var errors = submission.Validate();
        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errors));
            return await CreateValidationErrorResponseAsync(request, errors);
        }

        // ── Map and enqueue ─────────────────────────────────────────────────
        var feedbackMessage = submission.ToFeedbackMessage();

        try
        {
            await queueSender.SendAsync(feedbackMessage, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.IsTransient)
        {
            logger.LogError(ex, "Transient Service Bus error while sending feedback {FeedbackId}.", feedbackMessage.FeedbackId);
            return request.CreateResponse(HttpStatusCode.ServiceUnavailable);
        }
        catch (ServiceBusException ex)
        {
            logger.LogError(ex, "Service Bus error while sending feedback {FeedbackId}.", feedbackMessage.FeedbackId);
            return request.CreateResponse(HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while sending feedback {FeedbackId}.", feedbackMessage.FeedbackId);
            return request.CreateResponse(HttpStatusCode.InternalServerError);
        }

        logger.LogInformation("Enqueued feedback {FeedbackId} for store {StoreId}.", feedbackMessage.FeedbackId, feedbackMessage.StoreId);
        return request.CreateResponse(HttpStatusCode.OK);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData request, HttpStatusCode statusCode, string message)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { error = message }));
        return response;
    }

    private static async Task<HttpResponseData> CreateValidationErrorResponseAsync(
        HttpRequestData request, IReadOnlyList<string> errors)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { errors }));
        return response;
    }
}
