using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Persists processed feedback records to durable blob storage.
/// </summary>
public interface IFeedbackBlobStorageService
{
    /// <summary>
    /// Persists the provided processed feedback record and returns its blob path.
    /// </summary>
    Task<string> PersistAsync(ProcessedFeedbackRecord record, CancellationToken cancellationToken = default);
}
