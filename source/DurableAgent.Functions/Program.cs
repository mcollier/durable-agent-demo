using System.ComponentModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using DurableAgent.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Chat;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is not set.");

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetryWorkerService();

[Description("")]
static string GetCurrentUtcDateTime()
{
    return DateTime.UtcNow.ToString("o");
}

// Create a JSON schema for the expected output of the agent, which can be used for response validation and to help guide the agent's output format. This is optional but can improve reliability.
JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(FeedbackResult));

// Configure the chat response format to use the JSON schema. This tells the agent to structure its response according to the schema, which can help ensure consistent and parseable output.
ChatOptions chatOptions = new()
{
    Tools =
    [
        AIFunctionFactory.Create(GetCurrentUtcDateTime)
    ],
    Instructions = """
            You are the Customer Feedback Agent for Froyo Foundry.

            You process customer feedback events submitted in JSON format.
            You must analyze sentiment, detect risk signals, and produce a structured JSON response.
            You do not produce free-form text outside the required JSON schema.

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

            4. Generate a response message appropriate to the action:
                - Positive: Fun, warm, brand-aligned, but professional.
                - Neutral: Appreciative and include a 25% discount coupon.
                - Negative/Health: Sincere apology and indicate a representative will review and reach out. Do not offer a coupon in health/safety cases.

            5. Invoke tools only when needed:
                - Use GetCurrentUtcDateTime to validate the submittedAt timestamp and compute coupon expiration.
                - Use GenerateCouponCode when issuing a coupon.
                - Use ListFlavors to validate referenced flavors in complaints.
                - Use GetStoreDetails when escalating cases.
                - Use OpenCustomerServiceCase when action = OPEN_CASE.
                - Use RedactPII if the comment includes phone numbers, emails, or sensitive data before storing or referencing.

            6. Determinism requirement:
                - If tool results are provided, rely only on those results.
                - Do not invent store data, flavors, coupon codes, or case IDs.
                - Only use data from the input event and tool responses.

            7. Tone guidelines:
                - Friendly but professional.
                - Never dismiss health-related claims.
                - Never admit fault or legal liability.
                - Never speculate about medical causes.
                - Keep messages concise.

            Rules:
                - coupon must be null unless action = ISSUE_COUPON.
                - followUp.requiresHuman must be true if action = OPEN_CASE.
                - toolCalls must list only tools actually used.
                - confidence must be between 0.0 and 1.0.
                - Do not include explanations outside the JSON.
            """,
    ResponseFormat = ChatResponseFormat.ForJsonSchema(
        schema: schema,
        schemaName: "FeedbackResult",
        schemaDescription: "The result of analyzing a customer feedback message, including sentiment, risk assessment, and recommended action."
    )
};

// Create a chat client to be used by the agent.
// ChatClient chatClient = new AzureOpenAIClient(
//         new Uri(endpoint),
//         new DefaultAzureCredential())
//     .GetChatClient(deploymentName);

// Create the AI agent following standard Microsoft Agent Framework patterns. The agent will be injected into the activity function
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(new ChatClientAgentOptions()
    {
        Name = "CustomerServiceAgent",
        ChatOptions = chatOptions,
        
    });

builder.ConfigureDurableAgents(options =>
    options.AddAIAgent(agent)
);

var app = builder.Build();

app.Run();
