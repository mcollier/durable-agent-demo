using Azure.Messaging.ServiceBus;
using DurableAgent.Core.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Sends <see cref="FeedbackMessage"/> to the Service Bus
/// <c>inbound-feedback</c> queue using a <see cref="ServiceBusSender"/>.
/// </summary>
public sealed class ServiceBusFeedbackQueueSender : IFeedbackQueueSender
{
    private readonly ServiceBusSender _sender;

    public ServiceBusFeedbackQueueSender(ServiceBusClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        var queueName = Environment.GetEnvironmentVariable("SERVICEBUS_QUEUE_NAME")
            ?? throw new InvalidOperationException("SERVICEBUS_QUEUE_NAME environment variable is not set.");

        _sender = client.CreateSender(queueName);
    }

    /// <inheritdoc />
    public async Task SendAsync(FeedbackMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message))
        {
            ContentType = "application/json",
            MessageId = message.FeedbackId,
        };

        await _sender.SendMessageAsync(sbMessage, cancellationToken);
    }
}
