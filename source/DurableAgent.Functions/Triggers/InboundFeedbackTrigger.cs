using Azure.Messaging.ServiceBus;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Orchestrations;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// Receives messages from the inbound-feedback Service Bus queue and
/// starts a <see cref="FeedbackOrchestrator"/> for each message.
/// </summary>
public sealed class InboundFeedbackTrigger(ILogger<InboundFeedbackTrigger> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Function(nameof(InboundFeedbackTrigger))]
    public async Task RunAsync(
        [ServiceBusTrigger("%SERVICEBUS_QUEUE_NAME%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(durableClient);

        var feedback = message.Body.ToObjectFromJson<FeedbackMessage>(JsonOptions);

        if (feedback is null)
        {
            logger.LogWarning("Received null or empty feedback message. MessageId={MessageId}", message.MessageId);
            return;
        }

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(FeedbackOrchestrator),
            feedback,
            cancellation: cancellationToken);

        logger.LogInformation(
            "Started orchestration {InstanceId} for feedback {FeedbackId}",
            instanceId,
            feedback.Id);
    }
}
