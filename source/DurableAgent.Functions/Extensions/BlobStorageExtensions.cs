using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using DurableAgent.Functions.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Extensions;

/// <summary>
/// Extension methods for configuring feedback blob storage services.
/// </summary>
public static class BlobStorageExtensions
{
    private const string CustomerFeedbackBlobServiceUriKey = "CUSTOMER_FEEDBACK_BLOB_SERVICE_URI";
    private const string AzureWebJobsBlobServiceUriKey = "AzureWebJobsStorage__blobServiceUri";

    /// <summary>
    /// Adds Azure Blob Storage services for persisting processed feedback records.
    /// </summary>
    public static FunctionsApplicationBuilder AddFeedbackBlobStorage(this FunctionsApplicationBuilder builder)
    {
        string blobServiceUri = builder.Configuration[CustomerFeedbackBlobServiceUriKey]
            ?? builder.Configuration[AzureWebJobsBlobServiceUriKey]
            ?? throw new InvalidOperationException(
                $"Neither {CustomerFeedbackBlobServiceUriKey} nor {AzureWebJobsBlobServiceUriKey} configuration value is set.");

        TokenCredential credential = builder.Environment.IsDevelopment()
            ? new AzureCliCredential()
            : new DefaultAzureCredential();

        builder.Services.AddSingleton(_ => new BlobServiceClient(new Uri(blobServiceUri), credential));
        builder.Services.AddSingleton<IFeedbackBlobStorageService, AzureBlobFeedbackStorageService>();

        return builder;
    }
}
