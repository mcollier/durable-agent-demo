using System.ComponentModel;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Opens customer service cases for the AI agent.
/// </summary>
public static class OpenCustomerServiceCaseTool
{
    [Description("Opens a customer service case with the provided feedback ID and details.")]
    public static string OpenCustomerServiceCase(string feedbackId, string details)
    {
        // In a real implementation, this would call a case management system to open a new case and return the case ID. For this example, we'll return a placeholder value.
        return $"CASE-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
    }
}
