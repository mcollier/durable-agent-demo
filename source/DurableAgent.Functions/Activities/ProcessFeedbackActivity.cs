using DurableAgent.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Activities;

/// <summary>
/// Processes a single feedback message. This is the extensibility point
/// for adding real business logic (e.g., sentiment analysis, storage, notifications).
/// </summary>
public static class ProcessFeedbackActivity
{
    [Function(nameof(ProcessFeedbackActivity))]
    public static string Run(
        [ActivityTrigger] FeedbackMessage input,
        FunctionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(input);

        var logger = executionContext.GetLogger(nameof(ProcessFeedbackActivity));
        logger.LogInformation(
            "Processing feedback {FeedbackId}: {Content}",
            input.Id,
            input.Content);

        // Placeholder — replace with real business logic
        return $"Processed feedback '{input.Id}' at {DateTimeOffset.UtcNow:O}";
    }
}
