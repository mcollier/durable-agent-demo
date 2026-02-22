var builder = DistributedApplication.CreateBuilder(args);

var azureOpenAIEndpoint = builder.AddParameter("AZURE-OPENAI-ENDPOINT");
var azureOpenAIDeployment = builder.AddParameter("AZURE-OPENAI-DEPLOYMENT");
var taskHubName = builder.AddParameter("TASKHUB-NAME");
var durableTaskSchedulerConnectionString = builder.AddParameter("DURABLE-TASK-SCHEDULER-CONNECTION-STRING");
var applicationInsightsConnectionString = builder.AddParameter("APPLICATIONINSIGHTS-CONNECTION-STRING");
var serviceBusQueueName = builder.AddParameter("SERVICEBUS-QUEUE-NAME");
var serviceBusResourceGroup = builder.AddParameter("SERVICEBUS-RESOURCE-GROUP");
var serviceBusName = builder.AddParameter("SERVICEBUS-NAME");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite.WithBlobPort(10000)
            .WithQueuePort(10001)
            .WithTablePort(10002)
            .WithLifetime(ContainerLifetime.Persistent);
    });

var sb = builder.AddAzureServiceBus("messaging")
        .AsExisting(serviceBusName, serviceBusResourceGroup);

        // TODO: Figure out correct approach.
// var queue = sb.AddServiceBusQueue(serviceBusQueueName.Resource.Value);

var func = builder.AddAzureFunctionsProject<Projects.DurableAgent_Functions>("func")
    .WithHostStorage(storage)
    .WithReference(sb)
    .WaitFor(storage)
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", azureOpenAIEndpoint)
    .WithEnvironment("AZURE_OPENAI_DEPLOYMENT", azureOpenAIDeployment)
    .WithEnvironment("SERVICEBUS_QUEUE_NAME", serviceBusQueueName)
    // .WithEnvironment("ServiceBusConnection__fullyQualifiedNamespace", sb.Resource.ConnectionStringExpression)
    .WithEnvironment("TASKHUB_NAME", taskHubName)
    .WithEnvironment("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", durableTaskSchedulerConnectionString)
    .WithEnvironment("APPLICATIONINSIGHTS_CONNECTION_STRING", applicationInsightsConnectionString)
    .WithExternalHttpEndpoints();

var web = builder.AddProject<Projects.DurableAgent_Web>("web")
    .WithReference(func)
    .WithExternalHttpEndpoints()
    .WaitFor(func);

builder.Build().Run();
