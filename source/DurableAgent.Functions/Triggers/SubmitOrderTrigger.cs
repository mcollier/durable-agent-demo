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
/// HTTP POST endpoint that accepts order submissions, enqueues them for processing,
/// and returns 200 OK on success, 400 Bad Request for invalid JSON or validation
/// errors, 503 Service Unavailable for transient Service Bus failures, and 500
/// Internal Server Error for non-transient or unexpected errors.
/// </summary>
public sealed class SubmitOrderTrigger(ILogger<SubmitOrderTrigger> logger, IOrderQueueSender orderQueueSender)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Function(nameof(SubmitOrderTrigger))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── Deserialize request body ────────────────────────────────────────
        OrderRequest? order;
        try
        {
            order = await JsonSerializer.DeserializeAsync<OrderRequest>(
                request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON in request body.");
            return await CreateErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "Request body contains invalid JSON.");
        }

        if (order is null)
        {
            return await CreateErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "Request body is empty or null.");
        }

        var validationErrors = order.Validate();
        if (validationErrors.Count > 0)
        {
            logger.LogWarning("Validation failed: {Errors}", string.Join("; ", validationErrors));
            var response = request.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { errors = validationErrors }));
            return response;
        }

        logger.LogInformation("Received order {OrderReference}.", order.OrderReference);

        // ── Enqueue ─────────────────────────────────────────────────────────
        try
        {
            await orderQueueSender.SendAsync(order, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.IsTransient)
        {
            logger.LogError(ex, "Transient Service Bus error while sending order {OrderReference}.", order.OrderReference);
            return request.CreateResponse(HttpStatusCode.ServiceUnavailable);
        }
        catch (ServiceBusException ex)
        {
            logger.LogError(ex, "Service Bus error while sending order {OrderReference}.", order.OrderReference);
            return request.CreateResponse(HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while sending order {OrderReference}.", order.OrderReference);
            return request.CreateResponse(HttpStatusCode.InternalServerError);
        }

        logger.LogInformation("Enqueued order {OrderReference}.", order.OrderReference);
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
}
