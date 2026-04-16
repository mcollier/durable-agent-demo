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

        // Call the order-processing workflow HTTP endpoint exposed by the Durable Functions framework.
        var client = httpClientFactory.CreateClient("self");
        using var content = JsonContent.Create(order, options: JsonOptions);
        using var response = await client.PostAsync(
            "api/workflows/order-processing-workflow/run", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Workflow call failed for order {OrderReference}. Status={StatusCode}",
                order.OrderReference, response.StatusCode);
            throw new InvalidOperationException(
                $"Workflow returned {response.StatusCode} for order {order.OrderReference}");
        }

        logger.LogInformation(
            "Workflow completed for order {OrderReference}. Status={StatusCode}",
            order.OrderReference, response.StatusCode);
    }
}
