namespace DurableAgent.Core.Models;

/// <summary>
/// Customer details associated with a feedback submission.
/// </summary>
public sealed record CustomerInfo
{
    /// <summary>Customer's preferred/display name.</summary>
    public required string PreferredName { get; init; }

    /// <summary>Customer's first (given) name.</summary>
    public required string FirstName { get; init; }

    /// <summary>Customer's last (family) name.</summary>
    public required string LastName { get; init; }

    /// <summary>Customer's email address.</summary>
    public required string Email { get; init; }

    /// <summary>Customer's phone number.</summary>
    public required string PhoneNumber { get; init; }

    /// <summary>How the customer prefers to be contacted.</summary>
    public required ContactMethod PreferredContactMethod { get; init; }
}
