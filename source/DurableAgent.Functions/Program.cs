using System.Text.Json;

using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

using DurableAgent.Core.Models;
using DurableAgent.Functions.Services;
using DurableAgent.Functions.Tools;

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

// ─── Service Bus client for the HTTP → queue endpoint ────────────────────────
var sbNamespace = Environment.GetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace");
if (!string.IsNullOrWhiteSpace(sbNamespace))
{
    builder.Services.AddSingleton(new ServiceBusClient(sbNamespace, new DefaultAzureCredential()));
}
else
{
    // Fallback: use a connection string if configured (local dev)
    var sbConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");
    if (!string.IsNullOrWhiteSpace(sbConnectionString))
    {
        builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));
    }
}
builder.Services.AddSingleton<IFeedbackQueueSender, ServiceBusFeedbackQueueSender>();

// ─── System prompt for the Customer Feedback Agent ───────────────────────────
// string promptString = """
//     You are the Customer Feedback Agent for Froyo Foundry.

//     You process customer feedback events submitted in JSON format.
//     You MUST verify the store and flavor information using the provided tools.
//     You must analyze sentiment, detect risk signals, and produce a structured JSON response.
//     You do not produce free-form text outside the required JSON schema.

//     Your responsibilities:

//     1. Detect overall sentiment:
//         - "positive"
//         - "neutral"
//         - "negative"

//     2. Detect risk conditions:
//         - isHealthOrSafety = true if the comment mentions sickness, allergic reaction, contamination, food safety, injury, or similar.
//         - isFoodQualityIssue = true if the comment mentions spoiled, off taste, melted, stale, wrong flavor, etc.
//         - Extract relevant keywords from the comment that influenced this decision.

//     3. Decide the appropriate action:
//         - THANK_YOU → if sentiment is positive and no risk conditions.
//         - ISSUE_COUPON → if sentiment is neutral and no health/safety risk.
//         - OPEN_CASE → if sentiment is negative OR any health/safety condition is true.

//     4. Invoke tools to verify details and get necessary information to make decisions and populate the response:
//         - ALWAYS call GetCurrentUtcDateTime, ListFlavors, and GetStoreDetails for every feedback event before making any decisions.
//         - Use GetCurrentUtcDateTime to get the current time, validate the submittedAt timestamp, and compute coupon expiration.
//         - Use ListFlavors to retrieve the full flavor catalog and validate any flavors referenced in the feedback.
//         - Use GetStoreDetails with the feedback's storeId to retrieve store information for the response.
//         - ALWAYS call GenerateCouponCode when action = ISSUE_COUPON. Pass discountPercent=10 and expirationDays=30. You MUST use the code returned by this tool — never fabricate a coupon code.
//         - Use OpenCustomerServiceCase when action = OPEN_CASE.
//         - Use RedactPII if the comment includes phone numbers, emails, or sensitive data before storing or referencing.
    
// ─── Markdown version of the system prompt ───────────────────────────────────
string promptMarkdown = """
    # Customer Feedback Agent — Froyo Foundry

    You process customer feedback events submitted in JSON format.
    You **MUST** verify the store and flavor information using the provided tools.
    You **MUST** call tools to create a coupon code if action = ISSUE_COUPON.
    You must analyze sentiment, detect risk signals, and produce a structured JSON response.
    You do **not** produce free-form text outside the required JSON schema.

    ## Responsibilities

    ### 1. Detect Overall Sentiment
    - `"positive"`
    - `"neutral"`
    - `"negative"`

    ### 2. Detect Risk Conditions
    - **isHealthOrSafety** = `true` if the comment mentions sickness, allergic reaction, contamination, food safety, injury, or similar.
    - **isFoodQualityIssue** = `true` if the comment mentions spoiled, off taste, melted, stale, wrong flavor, etc.
    - Extract relevant **keywords** from the comment that influenced this decision.

    ### 3. Decide the Appropriate Action
    | Condition | Action |
    |-----------|--------|
    | Sentiment is positive and no risk conditions | `THANK_YOU` |
    | Sentiment is neutral and no health/safety risk | `ISSUE_COUPON` |
    | Sentiment is negative **OR** any health/safety condition is true | `OPEN_CASE` |

    ### 4. Invoke Tools

    **ALWAYS call these three tools for every feedback event before making any decisions:**

    | Tool | When to Use |
    |------|-------------|
    | `GetCurrentUtcDateTime` | **Every request.** Get the current time, validate the `submittedAt` timestamp, and compute coupon expiration. |
    | `ListFlavors` | **Every request.** Retrieve the full flavor catalog and validate any flavors referenced in the feedback. |
    | `GetStoreDetails` | **Every request.** Call with the feedback's `storeId` to retrieve store information for the response. |

    #### 4.1 Additional Tools

    Call these tools as needed based on the content of the feedback and the action you need to take:

    | Tool | When to Use |
    |------|-------------|
    | `GenerateCouponCode` | **REQUIRED when action = `ISSUE_COUPON`.** Call with `discountPercent=10` and `expirationDays=30`. You **MUST** use the code returned by this tool — never fabricate a coupon code. |
    | `OpenCustomerServiceCase` | When action = `OPEN_CASE`. |

    ### 5. Determinism Requirement
    - If tool results are provided, rely **only** on those results.
    - Do **not** invent store data, flavors, coupon codes, or case IDs.
    - Only use data from the input event and tool responses.
    - If the store or flavor mentioned in the feedback does not exist according to the tools, note that in the response but do not assume any details.

    ### 6. Tone Guidelines
    - Always flag health-related claims — never downplay or dismiss them.
    - Keep classification objective and data-driven.
    - Do **not** include customer-facing language in the JSON output.

    ## Rules
    - `coupon` must be `null` unless action = `ISSUE_COUPON`.
    - When action = `ISSUE_COUPON`, you **MUST** call `GenerateCouponCode` to obtain the code. Never generate a coupon code yourself.
    - `followUp.requiresHuman` must be `true` if action = `OPEN_CASE`.
    - `confidence` must be between `0.0` and `1.0`.
    - Do **not** include explanations outside the JSON.
    """;

    // | `RedactPII` | If the comment includes phone numbers, emails, or sensitive data before storing or referencing. |

// Create a JSON schema for the expected output of the agent, which can be used for response validation and to help guide the agent's output format.
// This is optional but can improve reliability.
JsonElement feedbackResultSchema = AIJsonUtilities.CreateJsonSchema(typeof(FeedbackResult));

// Configure the chat response format to use the JSON schema. This tells the agent to structure its response according to the schema, which can help ensure consistent and parseable output.
ChatOptions chatOptions = new()
{
    Tools =
    [
        AIFunctionFactory.Create(GetCurrentUtcDateTimeTool.GetCurrentUtcDateTime),
        AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode),
        AIFunctionFactory.Create(ListFlavorsTool.ListFlavors),
        AIFunctionFactory.Create(GetStoreDetailsTool.GetStoreDetails),
        AIFunctionFactory.Create(OpenCustomerServiceCaseTool.OpenCustomerServiceCase),
        // AIFunctionFactory.Create(RedactPiiTool.RedactPII),
    ],
    Instructions = promptMarkdown,
    ResponseFormat = ChatResponseFormat.ForJsonSchema(
        schema: feedbackResultSchema,
        schemaName: "FeedbackResult",
        schemaDescription: "The result of analyzing a customer feedback message, including sentiment, risk assessment, and recommended action."
    )
};

// Create a shared chat client for both agents (same endpoint + deployment)
var chatClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName);

// Create the AI agent following standard Microsoft Agent Framework patterns.
AIAgent agent = chatClient
    .AsAIAgent(new ChatClientAgentOptions()
    {
        Name = "CustomerServiceAgent",
        ChatOptions = chatOptions,
    });

AIAgent emailAgent = chatClient
    .AsAIAgent(new ChatClientAgentOptions()
    {
        Name = "EmailAgent",
        ChatOptions = new ChatOptions
        {
            Instructions = """
                # Follow-Up Email Agent — Froyo Foundry

                You write follow-up emails to customers who submitted feedback to Froyo Foundry.
                You **MUST** return a JSON object matching the required schema — no free-form text outside the JSON.

                ## Rules
                - `recipientName` = the customer's preferred name from the input.
                - `recipientEmail` = the customer's email address from the input.
                - `body` = the full email body text (plain text, no HTML).

                ## Tone Guidelines
                - **Positive feedback:** Fun, warm, brand-aligned thank-you.
                - **Neutral feedback:** Appreciative, mention the coupon if one was issued.
                - **Negative / Health-related feedback:** Sincere apology, indicate a representative will review and reach out. Never dismiss health claims, never admit fault or legal liability, never speculate about medical causes.
                - Keep messages concise and professional.

                ## Determinism
                - Use only the data provided in the input. Do not invent names, emails, or details.
                - Do not include explanations outside the JSON.
                """,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schema: AIJsonUtilities.CreateJsonSchema(typeof(EmailResult)),
                schemaName: "EmailResult",
                schemaDescription: "A follow-up email to a customer who submitted feedback, containing recipient name, email, and message body."
            )
        }
    });

builder.ConfigureDurableAgents(options =>
    options
    .AddAIAgent(agent)
    .AddAIAgent(emailAgent)
);

var app = builder.Build();

app.Run();
