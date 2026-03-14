namespace DurableAgent.Functions.Models;

/// <summary>
/// Represents the result of validating and normalizing an incoming order.
/// </summary>
public sealed record OrderIntakeResult
{
    /// <summary>True when the order passed intake validation.</summary>
    public required bool IsValid { get; init; }

    /// <summary>The normalized order payload when validation succeeds.</summary>
    public Order? Order { get; init; }

    /// <summary>Error message explaining why intake failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a normalized customer order ready for downstream processing.
/// </summary>
public sealed record Order
{
    /// <summary>Unique identifier for the order.</summary>
    public required string OrderId { get; init; }

    /// <summary>Name details for the customer who placed the order.</summary>
    public required OrderCustomerName CustomerName { get; init; }

    /// <summary>Email address for order communications.</summary>
    public required string CustomerEmail { get; init; }

    /// <summary>Shipping address for the order.</summary>
    public required OrderShippingAddress ShippingAddress { get; init; }

    /// <summary>Requested items in the order.</summary>
    public IReadOnlyList<OrderLineItem> LineItems { get; init; } = [];
}

/// <summary>
/// Represents the customer's name in a structured format.
/// </summary>
public sealed record OrderCustomerName
{
    /// <summary>Customer first name.</summary>
    public required string FirstName { get; init; }

    /// <summary>Customer middle name, if provided.</summary>
    public string? MiddleName { get; init; }

    /// <summary>Customer last name.</summary>
    public required string LastName { get; init; }
}

/// <summary>
/// Represents a United States shipping address.
/// </summary>
public sealed record OrderShippingAddress
{
    /// <summary>Primary street address.</summary>
    public required string StreetAddress { get; init; }

    /// <summary>Additional address details such as apartment or suite.</summary>
    public string? AddressLine2 { get; init; }

    /// <summary>City name.</summary>
    public required string City { get; init; }

    /// <summary>Two-letter USPS state or territory code.</summary>
    public required string State { get; init; }

    /// <summary>ZIP code in 5-digit or ZIP+4 format.</summary>
    public required string ZipCode { get; init; }
}

/// <summary>
/// Represents a single order line item.
/// </summary>
public sealed record OrderLineItem
{
    /// <summary>Stock-keeping unit for the requested item.</summary>
    public required string Sku { get; init; }

    /// <summary>Requested quantity for the SKU.</summary>
    public required int Quantity { get; init; }
}