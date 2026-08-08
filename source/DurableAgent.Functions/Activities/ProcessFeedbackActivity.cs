using DurableAgent.Core.Models;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Activities;

/// <summary>
/// Persists the processed feedback record after AI analysis completes.
/// </summary>
public static class ProcessFeedbackActivity
{
    [Function(nameof(ProcessFeedbackActivity))]
    public static async Task<string> RunAsync(
        [ActivityTrigger] ProcessedFeedbackRecord input,
        FunctionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Feedback);
        ArgumentNullException.ThrowIfNull(input.Analysis);
        ArgumentOutOfRangeException.ThrowIfLessThan(input.Feedback.Rating, 1, nameof(input.Feedback.Rating));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.Feedback.Rating, 5, nameof(input.Feedback.Rating));

        if (string.IsNullOrWhiteSpace(input.Feedback.FeedbackId))
        {
            throw new InvalidOperationException("Feedback.FeedbackId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Analysis.FeedbackId))
        {
            throw new InvalidOperationException("Analysis.FeedbackId is required.");
        }

        if (!string.Equals(input.Feedback.FeedbackId, input.Analysis.FeedbackId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Feedback.FeedbackId must exactly match Analysis.FeedbackId.");
        }

        var logger = executionContext.GetLogger(nameof(ProcessFeedbackActivity));
        var feedbackBlobStorageService = executionContext.InstanceServices.GetRequiredService<IFeedbackBlobStorageService>();

        logger.LogInformation(
            "Persisting processed feedback {FeedbackId}",
            input.Feedback.FeedbackId);

        string blobPath = await feedbackBlobStorageService.PersistAsync(input, executionContext.CancellationToken);

        logger.LogInformation(
            "Persisted processed feedback {FeedbackId} to {BlobPath}",
            input.Feedback.FeedbackId,
            blobPath);

        return $"Persisted feedback '{input.Feedback.FeedbackId}' to '{blobPath}'";
    }

    public static string Run(FeedbackMessage input, FunctionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfLessThan(input.Rating, 1, nameof(input.Rating));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.Rating, 5, nameof(input.Rating));

        var logger = executionContext.GetLogger(nameof(ProcessFeedbackActivity));
        logger.LogInformation(
            "Processing feedback {FeedbackId}: {Comment}",
            input.FeedbackId,
            input.Comment);

        return $"Processed feedback '{input.FeedbackId}' at {DateTimeOffset.UtcNow:O}";
    }
}
