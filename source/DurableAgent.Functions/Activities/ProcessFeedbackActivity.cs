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
        ArgumentOutOfRangeException.ThrowIfLessThan(input.Rating, 1, nameof(input.Rating));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.Rating, 5, nameof(input.Rating));

        var logger = executionContext.GetLogger(nameof(ProcessFeedbackActivity));
        logger.LogInformation(
            "Processing feedback {FeedbackId}: {Comment}",
            input.FeedbackId,
            input.Comment);

        // TODO: Replace with real business logic
        
        return $"Processed feedback '{input.FeedbackId}' at {DateTimeOffset.UtcNow:O}";
    }
}
