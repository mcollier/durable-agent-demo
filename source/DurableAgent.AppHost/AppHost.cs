var builder = DistributedApplication.CreateBuilder(args);

var azureOpenAIEndpoint = builder.AddParameter("AZURE-OPENAI-ENDPOINT");
var azureOpenAIDeployment = builder.AddParameter("AZURE-OPENAI-DEPLOYMENT");
var applicationInsightsConnectionString = builder.AddParameter("APPLICATIONINSIGHTS-CONNECTION-STRING");
var serviceBusResourceGroup = builder.AddParameter("SERVICEBUS-RESOURCE-GROUP");
var serviceBusName = builder.AddParameter("SERVICEBUS-NAME");
var senderEmailAddress = builder.AddParameter("SENDER-EMAIL-ADDRESS");
var recipientEmailAddress = builder.AddParameter("RECIPIENT-EMAIL-ADDRESS");
var emailServiceEndpoint = builder.AddParameter("EMAIL-SERVICE-ENDPOINT");

var feedbackQueueName = builder.Configuration["Parameters:FEEDBACK_QUEUE_NAME"]
    ?? throw new InvalidOperationException("Missing Parameters:FEEDBACK_QUEUE_NAME.");
var orderQueueName = builder.Configuration["Parameters:ORDER_QUEUE_NAME"]
    ?? throw new InvalidOperationException("Missing Parameters:ORDER_QUEUE_NAME.");

// Azure Durable Task Scheduler

#pragma warning disable ASPIREDURABLETASK001

var dtsScheduler = builder.AddDurableTaskScheduler("scheduler");
if (builder.ExecutionContext.IsRunMode)
{
    dtsScheduler.RunAsEmulator();
}
var dtsTaskHub = dtsScheduler.AddTaskHub("default");

#pragma warning restore ASPIREDURABLETASK001

// Azure Storage: emulator for local development, real Azure Storage when published.
var storage = builder.AddAzureStorage("storage");
if (builder.ExecutionContext.IsRunMode)
{
    storage.RunAsEmulator(azurite =>
    {
        azurite.WithBlobPort(10000)
               .WithQueuePort(10001)
               .WithTablePort(10002)
               .WithLifetime(ContainerLifetime.Persistent);
    });
}

    // Service bus (emulated).
    // var serviceBusName = "servicebus";
    // var serviceBus = builder.AddAzureServiceBus(serviceBusName)
    //     .RunAsEmulator(e => e
    //         .WithContainerName("your-servicebus-container")
    //         .WithLifetime(ContainerLifetime.Persistent)
    //     );
    // // Rename the SQL container created by the Service Bus emulator (hack):
    // var serviceBusSql = builder.Resources.OfType<ContainerResource>().Last(x => x.Name == $"{serviceBusName}-mssql");
    // serviceBusSql.Annotations.Add(new ContainerNameAnnotation { Name = "your-servicebus-mssql-container" });


// Set up Azure Service Bus. This assumes you have an existing Service Bus namespace and queue set up in Azure,
// and you are providing the necessary connection information via parameters.
var sb = builder.AddAzureServiceBus("messaging")
                // .RunAsEmulator();
            // .RunAsEmulator(emulator => 
            // {
            //     emulator.WithContainerName("your-servicebus-container")
            //             .WithLifetime(ContainerLifetime.Persistent);
            // });
            // var serviceBusSql = builder.Resources.OfType<ContainerResource>().Last(x => x.Name == $"messaging-mssql");
            // serviceBusSql.Annotations.Add(new ContainerNameAnnotation { Name = "your-servicebus-mssql-container" });
            
        .AsExisting(serviceBusName, serviceBusResourceGroup);

_ = sb.AddServiceBusQueue(feedbackQueueName);
_ = sb.AddServiceBusQueue(orderQueueName);

var func = builder.AddAzureFunctionsProject<Projects.DurableAgent_Functions>("func")
    .WithHostStorage(storage)
    .WithReference(sb)
    .WithEnvironment("AZURE_OPENAI_ENDPOINT", azureOpenAIEndpoint)
    .WithEnvironment("AZURE_OPENAI_DEPLOYMENT", azureOpenAIDeployment)
    .WithEnvironment("FEEDBACK_QUEUE_NAME", feedbackQueueName)
    .WithEnvironment("ORDER_QUEUE_NAME", orderQueueName)
    .WithEnvironment("APPLICATIONINSIGHTS_CONNECTION_STRING", applicationInsightsConnectionString)
    .WithEnvironment("OTEL_SOURCE_NAME", "DurableAgentDemo")
    .WithEnvironment("OTEL_SERVICE_NAME", "DurableAgentService")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("RECIPIENT_EMAIL_ADDRESS", recipientEmailAddress)
    .WithEnvironment("SENDER_EMAIL_ADDRESS", senderEmailAddress)
    .WithEnvironment("EMAIL_SERVICE_ENDPOINT", emailServiceEndpoint)
    .WithEnvironment("TASKHUB_NAME", dtsTaskHub.Resource.TaskHubName)
    .WithEnvironment("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", dtsScheduler)
    .WithExternalHttpEndpoints()
    .WaitFor(storage)
    .WaitFor(dtsScheduler)
    .WaitFor(sb);

// Self-reference so the Functions app can resolve its own HTTP endpoint via Aspire service discovery.
func.WithReference(func);

var web = builder.AddProject<Projects.DurableAgent_Web>("web")
    .WithReference(func)
    .WithExternalHttpEndpoints()
    .WaitFor(func);

builder.Build().Run();
