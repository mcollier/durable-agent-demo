namespace DurableAgent.Functions.Models;

/// <summary>
/// Configuration for Azure Communication Services email sending.
/// </summary>
public sealed class EmailSettings
{
    public required string RecipientEmailAddress { get; set; }
    public required string SenderEmailAddress { get; set; }
    public required string ServiceEndpoint { get; set; }
}
