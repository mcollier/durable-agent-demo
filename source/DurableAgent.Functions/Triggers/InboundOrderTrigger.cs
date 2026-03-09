using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// Receives messages from the inbound-orders Service Bus queue and
/// logs each order. No orchestration is started — this is a no-op stub.
/// </summary>
public sealed class InboundOrderTrigger(ILogger<InboundOrderTrigger> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Function(nameof(InboundOrderTrigger))]
    public Task RunAsync(
        [ServiceBusTrigger("%ORDER_QUEUE_NAME%", Connection = "messaging")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var order = message.Body.ToObjectFromJson<OrderRequest>(JsonOptions);

        if (order is null)
        {
            logger.LogWarning("Received null or empty order message. MessageId={MessageId}", message.MessageId);
            return Task.CompletedTask;
        }

        logger.LogInformation("Received order {OrderReference}.", order.OrderReference);
        return Task.CompletedTask;
    }
}
