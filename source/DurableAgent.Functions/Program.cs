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

using OpenAI.Chat;

using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is not set.");

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetryWorkerService();

[Description("Gets the current UTC date and time.")]
static string GetCurrentUtcDateTime()
{
    return DateTime.UtcNow.ToString("o");
}

[Description("Generates a unique coupon code with a specific format. ")]
static string GenerateCouponCode()
{    
    // In a real implementation, this would call a coupon service to generate a unique code and store the details. For this example, we'll return a placeholder value.
    return $"FRYOCUPON-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
}

[Description("Lists all available frozen yogurt flavors with allergen information.")]
static string ListFlavors()
{
    Flavor[] flavors =
    [
        new() { FlavorId = "flv-001", Name = "Mint Condition", Category = "Classic", ContainsDairy = true, ContainsNuts = false, Description = "Cool mint with dark chocolate chips. Zero bugs detected." },
        new() { FlavorId = "flv-002", Name = "Berry Blockchain Blast", Category = "Fruit", ContainsDairy = true, ContainsNuts = false, Description = "Strawberry + raspberry layered immutably for distributed sweetness." },
        new() { FlavorId = "flv-003", Name = "Cookie Container", Category = "Dessert", ContainsDairy = true, ContainsNuts = false, Description = "Chocolate cookie crumble isolated in its own delicious container." },
        new() { FlavorId = "flv-004", Name = "Recursive Raspberry", Category = "Fruit", ContainsDairy = false, ContainsNuts = false, Description = "Raspberry that calls itself again. And again. And again." },
        new() { FlavorId = "flv-005", Name = "Vanilla Exception", Category = "Classic", ContainsDairy = true, ContainsNuts = false, Description = "Simple. Predictable. Until it isn't." },
        new() { FlavorId = "flv-006", Name = "Null Pointer Pistachio", Category = "Nutty", ContainsDairy = true, ContainsNuts = true, Description = "Rich pistachio with zero reference errors." },
        new() { FlavorId = "flv-007", Name = "Java Jolt", Category = "Coffee", ContainsDairy = true, ContainsNuts = false, Description = "Strong coffee base compiled for performance." },
        new() { FlavorId = "flv-008", Name = "Peanut Butter Protocol", Category = "Nutty", ContainsDairy = true, ContainsNuts = true, Description = "A well-defined interface between chocolate and peanut butter." },
        new() { FlavorId = "flv-009", Name = "Cloud Caramel Cache", Category = "Dessert", ContainsDairy = true, ContainsNuts = false, Description = "Warm caramel layered for fast retrieval." },
        new() { FlavorId = "flv-010", Name = "AIçaí Bowl", Category = "Fruit", ContainsDairy = false, ContainsNuts = false, Description = "Smarter acai with machine-learned flavor balance." },
    ];

    return JsonSerializer.Serialize(flavors, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}

[Description("Gets details for a specific store by store ID. Returns JSON with store name, address, phone, email, manager, and timezone.")]
static string GetStoreDetails(string storeId)
{
    Store[] stores =
    [
        new() { StoreId = "store-001", Name = "Froyo Foundry - Hilliard", Address = new() { Street = "4182 Main St", City = "Hilliard", State = "OH", PostalCode = "43026" }, Phone = "+1-614-555-0101", Email = "hilliard@froyofoundry.com", Manager = new() { Name = "Emma Rodriguez", Email = "emma.rodriguez@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2022, 6, 15) },
        new() { StoreId = "store-002", Name = "Froyo Foundry - Dublin", Address = new() { Street = "6750 Perimeter Loop Rd", City = "Dublin", State = "OH", PostalCode = "43017" }, Phone = "+1-614-555-0102", Email = "dublin@froyofoundry.com", Manager = new() { Name = "Marcus Chen", Email = "marcus.chen@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2021, 9, 3) },
        new() { StoreId = "store-003", Name = "Froyo Foundry - Easton", Address = new() { Street = "4001 Easton Station", City = "Columbus", State = "OH", PostalCode = "43219" }, Phone = "+1-614-555-0103", Email = "easton@froyofoundry.com", Manager = new() { Name = "Priya Patel", Email = "priya.patel@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2023, 3, 22) },
        new() { StoreId = "store-004", Name = "Froyo Foundry - Short North", Address = new() { Street = "1122 N High St", City = "Columbus", State = "OH", PostalCode = "43201" }, Phone = "+1-614-555-0104", Email = "shortnorth@froyofoundry.com", Manager = new() { Name = "Daniel Kim", Email = "daniel.kim@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2020, 11, 10) },
        new() { StoreId = "store-005", Name = "Froyo Foundry - Polaris", Address = new() { Street = "1500 Polaris Pkwy", City = "Columbus", State = "OH", PostalCode = "43240" }, Phone = "+1-614-555-0105", Email = "polaris@froyofoundry.com", Manager = new() { Name = "Olivia Martinez", Email = "olivia.martinez@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2024, 1, 18) },
    ];

    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    var store = Array.Find(stores, s => s.StoreId.Equals(storeId, StringComparison.OrdinalIgnoreCase));

    return store is not null
        ? JsonSerializer.Serialize(store, jsonOptions)
        : JsonSerializer.Serialize(new { error = "Store not found", storeId }, jsonOptions);
}

[Description("Opens a customer service case with the provided feedback ID and details.")]
static string OpenCustomerServiceCase(string feedbackId, string details)
{  
    // In a real implementation, this would call a case management system to open a new case and return the case ID. For this example, we'll return a placeholder value.
    return $"CASE-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
}

[Description("Redacts personally identifiable information (PII) from the input.")]
static string RedactPII(string input)
{
    // In a real implementation, this would use a PII detection and redaction service. For this example, we'll just return the input with a note that it was redacted.
    return $"[REDACTED] {input}";
}

// Create a JSON schema for the expected output of the agent, which can be used for response validation and to help guide the agent's output format. This is optional but can improve reliability.
JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(FeedbackResult));

// Configure the chat response format to use the JSON schema. This tells the agent to structure its response according to the schema, which can help ensure consistent and parseable output.
ChatOptions chatOptions = new()
{
    Tools =
    [
        AIFunctionFactory.Create(GetCurrentUtcDateTime),
        AIFunctionFactory.Create(GenerateCouponCode),
        AIFunctionFactory.Create(ListFlavors),
        AIFunctionFactory.Create(GetStoreDetails),
        AIFunctionFactory.Create(OpenCustomerServiceCase),
        AIFunctionFactory.Create(RedactPII),
    ],
    // MaxOutputTokens = 10000,
    Instructions = """
            You are the Customer Feedback Agent for Froyo Foundry.

            You process customer feedback events submitted in JSON format.
            You must attempt to verify the store and flavor information using the provided tools, but you must not assume any details that are not included in the input or returned by the tools.
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

            5. Invoke tools to verify details and get necessary information to make decisions and populate the response:
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
                - If the store or flavor mentioned in the feedback does not exist according to the tools, note that in the response but do not assume any details.

            7. Tone guidelines:
                - Friendly but professional.
                - Never dismiss health-related claims.
                - Never admit fault or legal liability.
                - Never speculate about medical causes.
                - Keep messages concise.

            Rules:
                - coupon must be null unless action = ISSUE_COUPON.
                - followUp.requiresHuman must be true if action = OPEN_CASE.
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
