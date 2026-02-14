namespace DurableAgent.Core.Models;

/// <summary>
/// Represents an inbound feedback message received from the Service Bus queue.
/// </summary>
public sealed record FeedbackMessage
{
    /// <summary>Unique identifier for the feedback item.</summary>
    public required string Id { get; init; }

    /// <summary>The feedback content/body text.</summary>
    public required string Content { get; init; }

    /// <summary>UTC timestamp when the feedback was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
