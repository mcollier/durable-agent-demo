using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DurableAgent.Functions.Models;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Persists processed feedback records to Azure Blob Storage.
/// </summary>
public sealed class AzureBlobFeedbackStorageService(
    BlobServiceClient blobServiceClient,
    ILogger<AzureBlobFeedbackStorageService> logger) : IFeedbackBlobStorageService
{
    internal const string ContainerName = "customer-feedback";

    private readonly BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);

    public async Task<string> PersistAsync(ProcessedFeedbackRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        string feedbackId = ValidateRecord(record);
        string blobName = BuildBlobName(record.Feedback);
        string blobPath = BuildBlobPath(blobName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        BinaryData payload = BinaryData.FromBytes(
            JsonSerializer.SerializeToUtf8Bytes(record, ProcessedFeedbackRecordJsonSerializerOptions.Default));

        try
        {
            await blobClient.UploadAsync(
                payload,
                new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions
                    {
                        IfNoneMatch = ETag.All
                    },
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    }
                },
                cancellationToken);

            logger.LogInformation(
                "Persisted feedback blob for feedback {FeedbackId} to container {ContainerName} as {BlobName}",
                feedbackId,
                ContainerName,
                blobName);

            return blobPath;
        }
        catch (RequestFailedException ex) when (IsConditionalCreateFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Feedback blob already exists for feedback {FeedbackId} in container {ContainerName} as {BlobName}; validating existing payload",
                feedbackId,
                ContainerName,
                blobName);

            BlobDownloadResult existingBlob = await blobClient.DownloadContentAsync(cancellationToken);

            if (existingBlob.Content.ToMemory().Span.SequenceEqual(payload.ToMemory().Span))
            {
                logger.LogInformation(
                    "Existing feedback blob matched requested payload for feedback {FeedbackId} in container {ContainerName} as {BlobName}",
                    feedbackId,
                    ContainerName,
                    blobName);

                return blobPath;
            }

            logger.LogError(
                "FeedbackId collision detected for feedback {FeedbackId} in container {ContainerName} as {BlobName}",
                feedbackId,
                ContainerName,
                blobName);

            throw new InvalidOperationException(
                $"A different feedback payload already exists for feedback '{feedbackId}' at blob '{blobPath}'.",
                ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist feedback {FeedbackId} to container {ContainerName} as {BlobName}",
                feedbackId,
                ContainerName,
                blobName);
            throw;
        }
    }

    internal static string BuildBlobName(DurableAgent.Core.Models.FeedbackMessage feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        string feedbackId = ValidateFeedbackId(feedback.FeedbackId);

        if (feedback.SubmittedAt == default)
        {
            throw new InvalidOperationException("Feedback.SubmittedAt is required to build the blob path.");
        }

        string datePrefix = feedback.SubmittedAt.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{datePrefix}/{feedbackId}.json";
    }

    private static string BuildBlobPath(string blobName) => $"{ContainerName}/{blobName}";

    private static string ValidateRecord(ProcessedFeedbackRecord record)
    {
        ArgumentNullException.ThrowIfNull(record.Feedback);
        ArgumentNullException.ThrowIfNull(record.Analysis);

        string feedbackId = ValidateFeedbackId(record.Feedback.FeedbackId);

        if (string.IsNullOrWhiteSpace(record.Analysis.FeedbackId))
        {
            throw new InvalidOperationException("Analysis.FeedbackId is required.");
        }

        if (!string.Equals(feedbackId, record.Analysis.FeedbackId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Feedback.FeedbackId must exactly match Analysis.FeedbackId.");
        }

        return feedbackId;
    }

    private static string ValidateFeedbackId(string? feedbackId)
    {
        if (string.IsNullOrWhiteSpace(feedbackId))
        {
            throw new InvalidOperationException("Feedback.FeedbackId is required.");
        }

        if (feedbackId.Contains('/') || feedbackId.Contains('\\'))
        {
            throw new InvalidOperationException("Feedback.FeedbackId cannot contain path separators.");
        }

        return feedbackId;
    }

    private static bool IsConditionalCreateFailure(RequestFailedException ex) =>
        ex.Status == 409 || ex.Status == 412;
}
