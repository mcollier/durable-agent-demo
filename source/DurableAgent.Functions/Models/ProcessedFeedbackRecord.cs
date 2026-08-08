using DurableAgent.Core.Models;

namespace DurableAgent.Functions.Models;

/// <summary>
/// Represents the persisted feedback envelope containing the original submission and its AI analysis.
/// </summary>
public sealed record ProcessedFeedbackRecord
{
    /// <summary>The original customer feedback submission.</summary>
    public required FeedbackMessage Feedback { get; init; }

    /// <summary>The AI analysis produced for the feedback submission.</summary>
    public required FeedbackResult Analysis { get; init; }

    /// <summary>The durable orchestration instance identifier associated with this record.</summary>
    public string? OrchestrationInstanceId { get; init; }
}
