using Azure.Communication.Email;
using Azure.Core;
using Azure.Identity;
using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Extensions;

/// <summary>
/// Extension methods for configuring email services in the Azure Functions application.
/// </summary>
public static class EmailServiceExtensions
{
    /// <summary>
    /// Adds email service configuration and Azure Communication Services EmailClient to the dependency injection container.
    /// </summary>
    /// <param name="builder">The FunctionsApplicationBuilder to extend.</param>
    /// <returns>The FunctionsApplicationBuilder for method chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required environment variables (RECIPIENT_EMAIL_ADDRESS, SENDER_EMAIL_ADDRESS, or EMAIL_SERVICE_ENDPOINT) are not set.
    /// </exception>
    public static FunctionsApplicationBuilder AddEmailService(this FunctionsApplicationBuilder builder)
    {
        builder.Services.AddOptions<EmailSettings>()
            .Configure(opts =>
            {
                opts.RecipientEmailAddress = Environment.GetEnvironmentVariable("RECIPIENT_EMAIL_ADDRESS")
                    ?? throw new InvalidOperationException("RECIPIENT_EMAIL_ADDRESS environment variable is not set.");
                opts.SenderEmailAddress = Environment.GetEnvironmentVariable("SENDER_EMAIL_ADDRESS")
                    ?? throw new InvalidOperationException("SENDER_EMAIL_ADDRESS environment variable is not set.");
                opts.ServiceEndpoint = Environment.GetEnvironmentVariable("EMAIL_SERVICE_ENDPOINT")
                    ?? throw new InvalidOperationException("EMAIL_SERVICE_ENDPOINT environment variable is not set.");
            })
            .ValidateOnStart();

        string environmentName = builder.Environment.EnvironmentName;

        TokenCredential credential = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            ? new AzureCliCredential()
            : new DefaultAzureCredential();
        
        builder.Services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;
            return new EmailClient(new Uri(settings.ServiceEndpoint), credential);
        });

        return builder;
    }
}
