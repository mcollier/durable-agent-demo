namespace DurableAgent.Functions.Models;

/// <summary>
/// Input for the <see cref="Activities.SendEscalationEmailActivity"/>.
/// </summary>
public sealed record SendEscalationEmailInput
{
    /// <summary>Identifier of the feedback that triggered the escalation.</summary>
    public required string FeedbackId { get; init; }

    /// <summary>Case ID created by the AI agent for tracking.</summary>
    public required string CaseId { get; init; }

    /// <summary>Summary details for the email body.</summary>
    public required string Details { get; init; }
}
