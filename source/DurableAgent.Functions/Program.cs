using DurableAgent.Functions.Services;
using DurableAgent.Functions.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Add Aspire service defaults.
builder.AddServiceDefaults();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.AddAzureServiceBusClient(connectionName: "messaging");

builder.Services.AddSingleton<IFeedbackQueueSender, ServiceBusFeedbackQueueSender>();
builder.Services.AddSingleton<IOrderQueueSender, ServiceBusOrderQueueSender>();


builder.AddAzureOpenAI();

builder.AddEmailService();

builder.AddAgents();

builder.AddDurableAgents();

// Named HttpClient for calling the app's own workflow HTTP endpoints.
// Uses Aspire service discovery ("func" resolves to the app's own allocated endpoint).
builder.Services.AddHttpClient("self", client =>
{
    client.BaseAddress = new Uri("https+http://func/");
});

builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
