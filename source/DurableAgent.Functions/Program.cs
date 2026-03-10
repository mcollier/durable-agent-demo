using Azure.AI.OpenAI;
using Azure.Identity;

using DurableAgent.Core.Models;
using DurableAgent.Functions.Services;
using DurableAgent.Functions.Tools;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

// Get the Azure OpenAI endpoint.
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");

// Get the Azure OpenAI deployment name.
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is not set.");

// Configure OpenTelemetry for Aspire dashboard
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318";

string sourceName = Environment.GetEnvironmentVariable("OTEL_SOURCE_NAME") ?? "DurableAgentDemo";
string serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "DurableAgentService";

var builder = FunctionsApplication.CreateBuilder(args);

// Add Aspire service defaults.
builder.AddServiceDefaults();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.AddAzureServiceBusClient(connectionName: "messaging");

// ─── Service Bus client for the HTTP → queue endpoint ────────────────────────
// var sbNamespace = Environment.GetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace");
// if (!string.IsNullOrWhiteSpace(sbNamespace))
// {
//     builder.Services.AddSingleton(new ServiceBusClient(sbNamespace, new DefaultAzureCredential()));
// }
// else
// {
//     // Fallback: use a connection string if configured (local dev)
//     var sbConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnection");
//     if (!string.IsNullOrWhiteSpace(sbConnectionString))
//     {
//         builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));
//     }
// }

builder.Services.AddSingleton<IFeedbackQueueSender, ServiceBusFeedbackQueueSender>();
builder.Services.AddSingleton<IOrderQueueSender, ServiceBusOrderQueueSender>();

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

// Configure the chat response format to use the JSON schema. This tells the agent to structure its response according to the schema,
 // which can help ensure consistent and parseable output.
ChatOptions customerServiceAgentOptions = new()
{
    Tools =
    [
        AIFunctionFactory.Create(GetCurrentUtcDateTimeTool.GetCurrentUtcDateTime),
        AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode),
        AIFunctionFactory.Create(ListFlavorsTool.ListFlavors),
        AIFunctionFactory.Create(GetStoreDetailsTool.GetStoreDetails),
        AIFunctionFactory.Create(OpenCustomerServiceCaseTool.OpenCustomerServiceCase),
    ],
    Instructions = promptMarkdown,
    ResponseFormat = ChatResponseFormat.ForJsonSchema(
        schema: AIJsonUtilities.CreateJsonSchema(typeof(FeedbackResult)),
        schemaName: "FeedbackResult",
        schemaDescription: "The result of analyzing a customer feedback message, including sentiment, risk assessment, and recommended action."
    )
};

var environmentName =
    Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ??
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
    "Production";

bool isDevelopment = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(serviceName, serviceVersion: "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = environmentName,
        ["azure.openai.endpoint"] = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "unknown",
        ["service.instance.id"] = Environment.MachineName
    });

// Set up tracing.
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(sourceName)
    .AddSource("*Microsoft.Agents.AI") // Agent Framework telemetry
    .AddSource("*Microsoft.Extensions.AI") // Listen to the Experimental.Microsoft.Extensions.AI source for chat client telemetry.
    .AddSource("*Microsoft.Extensions.Agents*") // Listen to the Experimental.Microsoft.Extensions.Agents source for agent telemetry.
    .AddHttpClientInstrumentation() // Capture HTTP calls to OpenAI
    .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint))
    .Build();

// metrics
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter(sourceName)
    .AddMeter("*Microsoft.Agents.AI")
    .AddHttpClientInstrumentation() // HTTP client metrics
    .AddRuntimeInstrumentation() // .NET runtime metrics
    .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint))
    .Build();

// Create a shared chat client for both agents (same endpoint + deployment)
var chatClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: sourceName, configure: (cfg) => cfg.EnableSensitiveData = isDevelopment)
    .Build();

// Create the AI agent following standard Microsoft Agent Framework patterns.
AIAgent customerServiceAgent = chatClient
    .AsAIAgent(new ChatClientAgentOptions()
    {
        Name = "CustomerServiceAgent",
        ChatOptions = customerServiceAgentOptions,
    })
    .AsBuilder()
    .UseOpenTelemetry(sourceName: sourceName, configure: (cfg) => cfg.EnableSensitiveData = isDevelopment)
    .Build();

// Create a second agent that shares the same underlying chat client, but has different instructions and response format.
// This demonstrates how you can have multiple agents with different "personalities" and responsibilities, 
// while still sharing common configuration and telemetry.
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

// Define the agents for order processing. Will refactor later.
// Do I need this?
// builder.Services.AddSingleton(chatClient);
builder.Services.AddKeyedSingleton<IChatClient>("chat-model", chatClient);

var inventoryAgentInstructions = """
    # Inventory Agent — Froyo Foundry

    You analyze incoming orders and determine if they can be fulfilled based on available inventory. 
    You **MUST** call the provided tools to check inventory levels and gather relevant product information.
    You produce a structured JSON output with your findings — do not produce free-form text.

    ## Responsibilities

    1. Check if the ordered SKU is in stock.
    2. If not in stock, find substitute products that are similar in flavor profile.
    3. Provide a structured output indicating whether the order can be fulfilled or not.
    4. If unable to fulfill, create a 25% discount coupon for the customer.

    ## Tools

    | Tool | Purpose |
    |------|---------|
    | `CheckInventory(Sku)` | Returns available quantity for the given SKU. |

    ## Output Format

    Your response must strictly follow this JSON schema:

    ```json
    {
        "orderId": "string",
        "customerEmail": "string",
        "items": [
            {
                "sku": "string",
                "productName": "string",
                "requestedQty": 0,
                "availableQty": 0,
                "fulfillableQty": 0,
                "shortfallQty": 0
            }
        ],
        "canFullyFulfill": false,
        "shouldGenerateCoupon": false,
        "coupon": {
            "code": "string",
            "discountPercent": 0
        }
    }
    ```

    - `coupon` must be `null` unless `shouldGenerateCoupon` is `true`.
    - `fulfillableQty` = min(`requestedQty`, `availableQty`).
    - `shortfallQty` = `requestedQty` − `fulfillableQty`.
    - `canFullyFulfill` = `true` only if `shortfallQty` is 0 for all items.
    - `shouldGenerateCoupon` = `true` when any item has a shortfall.

    ## Determinism Requirement
    - Rely solely on tool outputs for inventory data and product details.
    - Do not fabricate information about stock levels or product attributes.
    """;

var inventoryAgent = builder.AddAIAgent(
    name: "InventoryAgent",
    (sp, key) =>
    {
        var chatClient = sp.GetRequiredKeyedService<IChatClient>("chat-model");

        AIAgent agent = new ChatClientAgent(
            options: new ChatClientAgentOptions
            {
                Name = key,
                ChatOptions = new ChatOptions
                {
                    Tools =
                    [
                        AIFunctionFactory.Create(CheckInventoryTool.CheckInventory)
                    ],
                    Instructions = inventoryAgentInstructions,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(InventoryAnalysisResult)),
                        schemaName: "InventoryAnalysisResult",
                        schemaDescription: "The result of analyzing an order's inventory status, including fulfillment details and any coupon generation decision."
                    )
                }
            },
            chatClient: chatClient
        );
        return agent;
    }
);

var orderEmailAgent = builder.AddAIAgent(
    name: "OrderEmailAgent",
    (sp, key) =>
    {
        var chatClient = sp.GetRequiredKeyedService<IChatClient>("chat-model");

        AIAgent agent = new ChatClientAgent(
            options: new ChatClientAgentOptions
            {
                Name = key,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are the Email Agent in an order processing workflow.

                        Your job is to generate a customer email based on the inventory assessment produced by the Inventory Agent.

                        You do NOT:
                        - check inventory
                        - change quantities
                        - invent business outcomes
                        - create new coupon codes unless provided

                        Use the input inventory result as the source of truth.

                        ## Responsibilities

                        1. Read the inventory assessment.
                        2. Determine the fulfillment scenario.
                        3. Generate a clear and friendly customer email.
                        4. Return structured JSON.

                        ## Email Scenarios

                        ### Full Fulfillment
                        If canFullyFulfill = true
                        - confirm the full order will ship soon
                        - positive tone

                        ### Partial Fulfillment
                        If some items are available but not the full quantity
                        - explain that available items will ship
                        - explain remaining items could not be fulfilled
                        - include coupon if provided

                        ### No Fulfillment
                        If no items are available
                        - explain the order cannot be fulfilled
                        - include coupon if provided

                        ## Writing Style

                        Emails must be:
                        - clear
                        - concise
                        - polite
                        - customer-friendly

                        Do not mention internal systems, agents, tools, or workflows.

                        ## Output Requirements

                        Return valid JSON only.

                        Structure:

                        {
                            "orderId": "string",
                            "emailType": "FullFulfillment | PartialFulfillment | NoFulfillment",
                            "emailSubject": "string",
                            "emailBody": "string"
                        }
                        """,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(EmailResult)),
                        schemaName: "EmailResult",
                        schemaDescription: "A follow-up email to a customer regarding their order fulfillment status, containing recipient name, email, and message body."
                    )
                }
            },
            chatClient: chatClient
        );
        
        return agent;
    }
);

// The workflow will consist of two agents:
// 1. Inventory Agent - responsible for gathering details on the avaialble inventory and if the order can be fulfilled. This agent will call tools to check inventory levels. It will produce a structured output with its findings.
// 2. Customer Messaging Agent - responsible for crafting the message to the customer based on the output of the Inventory Agent. It will produce the final message

// var contextBuilderAgent = builder.AddAIAgent(
//     name: "ContextBuilderAgent",
//     (sp, key) =>
//     {
//         var chatClient = sp.GetRequiredKeyedService<IChatClient>("chat-model");

//         AIAgent agent = new ChatClientAgent(
//             options: new ChatClientAgentOptions
//             {
//                 Name = key,
//                 ChatOptions = new ChatOptions
//                 {
//                     Tools = [],
//                     Instructions = orderShortfallAgentInstructions,
//                     ResponseFormat = ChatResponseFormat.ForJsonSchema(
//                         schema: AIJsonUtilities.CreateJsonSchema(typeof(ContextBuilderOutput)),
//                         schemaName: "ContextBuilderOutput",
//                         schemaDescription: "The structured context output produced by the Context Builder Agent, containing all relevant factual information about the order shortfall scenario."
//                     )
//                 }
//             },
//             chatClient: chatClient);

//         return agent;
//     });


var workflowAsAgent = builder.AddWorkflow("order-processing-workflow", (sp, key) =>
{
    var inventoryAgent = sp.GetRequiredKeyedService<AIAgent>("InventoryAgent");
    var orderEmailAgent = sp.GetRequiredKeyedService<AIAgent>("OrderEmailAgent");

    // add other agents to the workflow

    return AgentWorkflowBuilder.BuildSequential(key, [inventoryAgent, orderEmailAgent]);
}).AddAsAIAgent();



builder.ConfigureFunctionsWebApplication();

builder.ConfigureDurableAgents(options =>
    options
    .AddAIAgent(customerServiceAgent)
    .AddAIAgent(emailAgent)
);

builder.Build().Run();