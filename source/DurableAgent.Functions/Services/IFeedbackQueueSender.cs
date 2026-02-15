using DurableAgent.Core.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Sends <see cref="FeedbackMessage"/> instances to the inbound-feedback queue.
/// </summary>
public interface IFeedbackQueueSender
{
    /// <summary>
    /// Serializes and sends the given <paramref name="message"/> to the Service Bus queue.
    /// </summary>
    Task SendAsync(FeedbackMessage message, CancellationToken cancellationToken = default);
}
