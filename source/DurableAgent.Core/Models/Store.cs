namespace DurableAgent.Core.Models;

/// <summary>
/// Represents a Froyo Foundry store location.
/// </summary>
public sealed record Store
{
    /// <summary>Unique identifier for the store (e.g., "store-001").</summary>
    public required string StoreId { get; init; }

    /// <summary>Display name of the store.</summary>
    public required string Name { get; init; }

    /// <summary>Physical address of the store.</summary>
    public required StoreAddress Address { get; init; }

    /// <summary>Store phone number.</summary>
    public required string Phone { get; init; }

    /// <summary>Store email address.</summary>
    public required string Email { get; init; }

    /// <summary>Store manager contact information.</summary>
    public required StoreManager Manager { get; init; }

    /// <summary>IANA timezone identifier for the store (e.g., "America/New_York").</summary>
    public required string Timezone { get; init; }

    /// <summary>Date the store opened.</summary>
    public required DateOnly OpenedDate { get; init; }
}

/// <summary>
/// Physical address of a store.
/// </summary>
public sealed record StoreAddress
{
    /// <summary>Street address.</summary>
    public required string Street { get; init; }

    /// <summary>City name.</summary>
    public required string City { get; init; }

    /// <summary>State or province abbreviation.</summary>
    public required string State { get; init; }

    /// <summary>Postal / ZIP code.</summary>
    public required string PostalCode { get; init; }
}

/// <summary>
/// Store manager contact information.
/// </summary>
public sealed record StoreManager
{
    /// <summary>Manager's full name.</summary>
    public required string Name { get; init; }

    /// <summary>Manager's email address.</summary>
    public required string Email { get; init; }
}
