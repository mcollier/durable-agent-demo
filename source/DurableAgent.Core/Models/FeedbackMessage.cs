namespace DurableAgent.Core.Models;

/// <summary>
/// Represents an inbound feedback message received from the Service Bus queue.
/// </summary>
public sealed record FeedbackMessage
{
    /// <summary>Unique identifier for the feedback item.</summary>
    public required string FeedbackId { get; init; }

    /// <summary>UTC timestamp when the feedback was submitted.</summary>
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Identifier of the store where the feedback originated.</summary>
    public required string StoreId { get; init; }

    /// <summary>Identifier of the associated order.</summary>
    public required string OrderId { get; init; }

    /// <summary>Customer details associated with the feedback.</summary>
    public required CustomerInfo Customer { get; init; }

    /// <summary>Channel through which the feedback was submitted (e.g., "kiosk", "web", "app").</summary>
    public required string Channel { get; init; }

    /// <summary>Customer rating (1–5).</summary>
    public required int Rating { get; init; }

    /// <summary>Free-text feedback comment.</summary>
    public required string Comment { get; init; }

    /// <summary>Identifier of the flavor the customer tried (optional).</summary>
    public string? FlavorId { get; init; }
}
