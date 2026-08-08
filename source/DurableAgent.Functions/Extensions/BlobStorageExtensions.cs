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
    private const string AzureWebJobsStorageConnectionStringKey = "AzureWebJobsStorage";
    private const string AzureWebJobsBlobServiceUriKey = "AzureWebJobsStorage__blobServiceUri";

    /// <summary>
    /// Adds Azure Blob Storage services for persisting processed feedback records.
    /// </summary>
    public static FunctionsApplicationBuilder AddFeedbackBlobStorage(this FunctionsApplicationBuilder builder)
    {
        builder.Services.AddSingleton(_ => CreateBlobServiceClient(builder));
        builder.Services.AddSingleton<IFeedbackBlobStorageService, AzureBlobFeedbackStorageService>();

        return builder;
    }

    private static BlobServiceClient CreateBlobServiceClient(FunctionsApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Environment.IsDevelopment())
        {
            string connectionString = builder.Configuration[AzureWebJobsStorageConnectionStringKey]
                ?? throw new InvalidOperationException(
                    $"Development storage requires the {AzureWebJobsStorageConnectionStringKey} configuration value.");

            return new BlobServiceClient(connectionString);
        }

        string blobServiceUri = builder.Configuration[AzureWebJobsBlobServiceUriKey]
            ?? throw new InvalidOperationException(
                $"Production storage requires the {AzureWebJobsBlobServiceUriKey} configuration value.");

        TokenCredential credential = new DefaultAzureCredential();
        return new BlobServiceClient(new Uri(blobServiceUri), credential);
    }
}
