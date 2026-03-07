var builder = DistributedApplication.CreateBuilder(args);

const string DtsEmulatorTaskHubName = "default";

var azureOpenAIEndpoint = builder.AddParameter("AZURE-OPENAI-ENDPOINT");
var azureOpenAIDeployment = builder.AddParameter("AZURE-OPENAI-DEPLOYMENT");
var taskHubName = builder.AddParameter("TASKHUB-NAME");
var durableTaskSchedulerConnectionString = builder.AddParameter("DURABLE-TASK-SCHEDULER-CONNECTION-STRING");
var applicationInsightsConnectionString = builder.AddParameter("APPLICATIONINSIGHTS-CONNECTION-STRING");
var serviceBusResourceGroup = builder.AddParameter("SERVICEBUS-RESOURCE-GROUP");
var serviceBusName = builder.AddParameter("SERVICEBUS-NAME");

var queueName = builder.Configuration["Parameters:SERVICEBUS_QUEUE_NAME"]
    ?? throw new InvalidOperationException("Missing Parameters:SERVICEBUS_QUEUE_NAME.");

var useDtsEmulator = ResolveDurableTaskSchedulerMode(
    builder.Configuration["DurableTaskScheduler:Mode"],
    builder.ExecutionContext.IsRunMode);

// Set up Azure Storage Emulator (Azurite) for local development and testing.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite.WithBlobPort(10000)
            .WithQueuePort(10001)
            .WithTablePort(10002)
            .WithLifetime(ContainerLifetime.Persistent);
    });

// Set up Azure Service Bus. This assumes you have an existing Service Bus namespace and queue set up in Azure,
// and you are providing the necessary connection information via parameters.
var sb = builder.AddAzureServiceBus("messaging")
        .AsExisting(serviceBusName, serviceBusResourceGroup);

_ = sb.AddServiceBusQueue(queueName);

var func = builder.AddAzureFunctionsProject<Projects.DurableAgent_Functions>("func")
    .WithHostStorage(storage)
    .WithReference(sb)
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", azureOpenAIEndpoint)
    .WithEnvironment("AZURE_OPENAI_DEPLOYMENT", azureOpenAIDeployment)
    .WithEnvironment("SERVICEBUS_QUEUE_NAME", queueName)
    .WithEnvironment("APPLICATIONINSIGHTS_CONNECTION_STRING", applicationInsightsConnectionString)
    .WithEnvironment("OTEL_SOURCE_NAME", "DurableAgentDemo")
    .WithEnvironment("OTEL_SERVICE_NAME", "DurableAgentService")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithExternalHttpEndpoints();

if (useDtsEmulator)
{
    // Local runs default to the DTS emulator unless configuration explicitly overrides the mode.
    var dts = builder.AddContainer("dts", "mcr.microsoft.com/dts/dts-emulator", "latest")
        .WithEndpoint(name: "grpc", targetPort: 8080)
        .WithHttpEndpoint(name: "http", targetPort: 8081)
        .WithHttpEndpoint(name: "dashboard", targetPort: 8082);

    var grpcEndpoint = dts.GetEndpoint("grpc");
    var dtsConnectionString = ReferenceExpression.Create(
        $"Endpoint=http://{grpcEndpoint.Property(EndpointProperty.Host)}:{grpcEndpoint.Property(EndpointProperty.Port)};Authentication=None");

    func = func
        .WithEnvironment("TASKHUB_NAME", DtsEmulatorTaskHubName)
        .WithEnvironment("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", dtsConnectionString)
        .WaitFor(dts);
}
else
{
    func = func
        .WithEnvironment("TASKHUB_NAME", taskHubName)
        .WithEnvironment("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", durableTaskSchedulerConnectionString);
}

var web = builder.AddProject<Projects.DurableAgent_Web>("web")
    .WithReference(func)
    .WithExternalHttpEndpoints()
    .WaitFor(func);

builder.Build().Run();

// Centralize DTS backend selection so the Functions app can keep consuming a single
// connection-string setting regardless of whether AppHost is running locally or publishing.
static bool ResolveDurableTaskSchedulerMode(string? configuredMode, bool isRunMode)
{
    if (string.IsNullOrWhiteSpace(configuredMode))
    {
        // When no override is configured, follow Aspire's execution context:
        // local run mode uses the emulator and non-run contexts use Azure.
        return isRunMode;
    }

    return configuredMode.Trim().ToLowerInvariant() switch
    {
        // Auto preserves the default behavior while still allowing explicit overrides
        // for local runs that need to target the provisioned Azure scheduler.
        "auto" => isRunMode,
        "emulator" => true,
        "azure" => false,
        _ => throw new InvalidOperationException(
            "Unsupported DurableTaskScheduler:Mode value. Supported values are Auto, Emulator, or Azure.")
    };
}
