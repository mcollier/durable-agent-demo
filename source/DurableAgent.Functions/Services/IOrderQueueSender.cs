using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Sends <see cref="OrderRequest"/> instances to the inbound-orders queue.
/// </summary>
public interface IOrderQueueSender
{
    /// <summary>
    /// Serializes and sends the given <paramref name="order"/> to the Service Bus queue.
    /// </summary>
    Task SendAsync(OrderRequest order, CancellationToken cancellationToken = default);
}
