using System.Text.Json.Serialization;

namespace DurableAgent.Core.Models;

/// <summary>
/// Preferred contact method for a customer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContactMethod
{
    /// <summary>Contact via email.</summary>
    Email,

    /// <summary>Contact via phone.</summary>
    Phone
}
