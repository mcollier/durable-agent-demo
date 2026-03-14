namespace DurableAgent.Functions.Models;

public sealed record CustomerMessageResult
{
    /// <summary>Identifier of the associated order.</summary>
    public string? OrderId { get; init; }
    /// <summary>Message to be sent to the customer.</summary>
    public string? Message { get; init; }
}