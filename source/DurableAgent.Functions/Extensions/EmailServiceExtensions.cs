using Azure.Communication.Email;
using Azure.Identity;
using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Extensions;

public static class EmailServiceExtensions
{
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

        builder.Services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;
            return new EmailClient(new Uri(settings.ServiceEndpoint), new DefaultAzureCredential());
        });

        return builder;
    }
}
