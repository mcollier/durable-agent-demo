using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;

namespace DurableAgent.Functions.Extensions
{
    public static class AIServiceExtensions
    {
        public static FunctionsApplicationBuilder AddAzureOpenAI(this FunctionsApplicationBuilder builder)
        {
            // Get the Azure OpenAI endpoint.
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");

            // Get the Azure OpenAI deployment name.
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is not set.");

            string sourceName = builder.Environment.ApplicationName;

            var environmentName =
                Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ??
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                "Production";

            bool isDevelopment = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);
            
            IChatClient chatClient = new AzureOpenAIClient(
                new Uri(endpoint),
                new DefaultAzureCredential())
                .GetChatClient(deploymentName)
                .AsIChatClient()
                .AsBuilder()
                .UseOpenTelemetry(sourceName: sourceName, configure: (cfg) => cfg.EnableSensitiveData = isDevelopment)
                .Build();

            builder.Services.AddChatClient(chatClient);

            builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environmentName,
                    ["azure.openai.endpoint"] = endpoint,
                    ["service.instance.id"] = Environment.MachineName
                });
             });

            return builder;
        }
    }
}