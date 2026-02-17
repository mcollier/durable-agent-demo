namespace DurableAgent.Functions.Models;

/// <summary>
/// Input for the <see cref="Activities.SendCustomerEmailActivity"/>.
/// </summary>
public sealed record SendCustomerEmailInput
{
    /// <summary>Identifier of the feedback that triggered the email.</summary>
    public required string FeedbackId { get; init; }

    /// <summary>Case ID created by the AI agent for tracking.</summary>
    public required string CaseId { get; init; }

    /// <summary>Display name of the email recipient (the customer).</summary>
    public required string RecipientName { get; init; }

    /// <summary>Email address of the recipient.</summary>
    public required string RecipientEmail { get; init; }

    /// <summary>Body content of the email to send.</summary>
    public required string Body { get; init; }
}
