using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Sends <see cref="OrderRequest"/> to the Service Bus
/// <c>inbound-orders</c> queue using a <see cref="ServiceBusSender"/>.
/// </summary>
public sealed class ServiceBusOrderQueueSender : IOrderQueueSender
{
    private readonly ServiceBusSender _sender;

    public ServiceBusOrderQueueSender(ServiceBusClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        var queueName = Environment.GetEnvironmentVariable("ORDER_QUEUE_NAME")
            ?? throw new InvalidOperationException("ORDER_QUEUE_NAME environment variable is not set.");

        _sender = client.CreateSender(queueName);
    }

    /// <inheritdoc />
    public async Task SendAsync(OrderRequest order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(order))
        {
            ContentType = "application/json",
            MessageId = order.OrderReference,
        };

        await _sender.SendMessageAsync(sbMessage, cancellationToken);
    }
}
