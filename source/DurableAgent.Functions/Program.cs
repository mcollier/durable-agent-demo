using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is not set.");

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetryWorkerService();

// Create the AI agent following standard Microsoft Agent Framework patterns. The agent will be injected into the activity function
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: """
            You are the Customer Feedback Agent for Froyo Foundry.

            You process customer feedback events submitted in JSON format.
            You must analyze sentiment, detect risk signals, and produce a structured JSON response.

            Your responsibilities:

            1. Detect overall sentiment:
               - "positive"
               - "neutral"
               - "negative"

            2. Detect risk conditions:
               - isHealthOrSafety = true if the comment mentions sickness, allergic reaction, contamination, food safety, injury, or similar.
               - isFoodQualityIssue = true if the comment mentions spoiled, off taste, melted, stale, wrong flavor, etc.
               - Extract relevant keywords from the comment that influenced this decision.

            3. Decide the appropriate action:
               - THANK_YOU → if sentiment is positive and no risk conditions.
               - ISSUE_COUPON → if sentiment is neutral and no health/safety risk.
               - OPEN_CASE → if sentiment is negative OR any health/safety condition is true.
            """,
        name: "CustomerServiceAgent");

builder.ConfigureDurableAgents(options =>
    options.AddAIAgent(agent)
);

var app = builder.Build();

app.Run();
