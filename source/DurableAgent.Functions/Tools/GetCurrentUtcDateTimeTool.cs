using System.ComponentModel;

namespace DurableAgent.Functions.Tools;

/// <summary>
/// Provides the current UTC date/time for the AI agent.
/// </summary>
public static class GetCurrentUtcDateTimeTool
{
    [Description("Gets the current UTC date and time.")]
    public static string GetCurrentUtcDateTime()
    {
        return DateTime.UtcNow.ToString("o");
    }
}
