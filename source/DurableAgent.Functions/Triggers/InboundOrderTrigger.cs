using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// Receives order messages from the inbound-orders Service Bus queue, runs the order-processing
/// AI agent workflow to determine fulfilment, and sends a follow-up email to the customer
/// via Azure Communication Services.
/// </summary>
public sealed class InboundOrderTrigger(ILogger<InboundOrderTrigger> logger,
                                        IHttpClientFactory httpClientFactory
                                        )
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Function(nameof(InboundOrderTrigger))]
    public async Task RunAsync(
        [ServiceBusTrigger("%ORDER_QUEUE_NAME%", Connection = "messaging")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var order = message.Body.ToObjectFromJson<OrderRequest>(JsonOptions);

        if (order is null)
        {
            logger.LogWarning("Received null or empty order message. MessageId={MessageId}", message.MessageId);
            return;
        }

        logger.LogInformation("Received order {OrderReference}.", order.OrderReference);

        // Host the handoff workflow as a durable agent because the durable workflow runner cannot
        // supply the separate TurnToken required to start a handoff orchestration.
        var client = httpClientFactory.CreateClient("self");
        string orderJson = JsonSerializer.Serialize(order, JsonOptions);
        using var content = JsonContent.Create(new { message = orderJson });
        using var response = await client.PostAsync(
            "api/agents/order-processing-workflow/run?wait=false", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var normalizedResponseBody = string.IsNullOrWhiteSpace(responseBody) ? null : responseBody;

            logger.LogError(
                "Workflow call failed for order {OrderReference}. Status={StatusCode}. ResponseBody={ResponseBody}",
                order.OrderReference, response.StatusCode, normalizedResponseBody);

            throw new InvalidOperationException(
                normalizedResponseBody is null
                    ? $"Workflow returned {response.StatusCode} for order {order.OrderReference}"
                    : $"Workflow returned {response.StatusCode} for order {order.OrderReference}. Response body: {normalizedResponseBody}");
        }

        logger.LogInformation(
            "Workflow started for order {OrderReference}. Status={StatusCode}",
            order.OrderReference, response.StatusCode);
    }
}
