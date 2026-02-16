namespace DurableAgent.Core.Models;

/// <summary>
/// Represents the structured email output produced by the EmailAgent.
/// </summary>
public sealed record EmailResult
{
    /// <summary>Display name of the email recipient (the customer).</summary>
    public required string RecipientName { get; init; }

    /// <summary>Email address of the recipient.</summary>
    public required string RecipientEmail { get; init; }

    /// <summary>Body content of the email to send.</summary>
    public required string Body { get; init; }
}
