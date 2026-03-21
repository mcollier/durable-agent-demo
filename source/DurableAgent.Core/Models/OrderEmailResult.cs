namespace DurableAgent.Core.Models;

/// <summary>
/// Represents the email to be sent to a customer regarding their order status.
/// </summary>
public sealed record OrderEmailResult
{
    /// <summary>Unique identifier of the order this email relates to.</summary>
    public required string OrderId { get; init; }

    /// <summary>Subject line of the email.</summary>
    public required string EmailSubject { get; init; }

    /// <summary>Plain-text body of the email.</summary>
    public required string EmailBody { get; init; }

    /// <summary>Categorizes the nature of the email (e.g., FullFulfillment, PartialFulfillment, OutOfStock).</summary>
    public required string EmailType { get; init; }
}
