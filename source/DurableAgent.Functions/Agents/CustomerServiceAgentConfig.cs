using DurableAgent.Core.Models;
using DurableAgent.Functions.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class CustomerServiceAgentConfig
{
    public const string AgentName = "CustomerServiceAgent";
    public const string SystemPrompt = """
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

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        // Get the IChatClient from the DI container
        var chatClient = builder.Services.BuildServiceProvider().GetRequiredService<IChatClient>();

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
            Instructions = SystemPrompt,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schema: AIJsonUtilities.CreateJsonSchema(typeof(FeedbackResult)),
                schemaName: "FeedbackResult",
                schemaDescription: "The result of analyzing a customer feedback message, including sentiment, risk assessment, and recommended action."
            )
        };

        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                AIAgent agent = new ChatClientAgent(
                    options: new ChatClientAgentOptions()
                    {
                        Name = AgentName,
                        ChatOptions = customerServiceAgentOptions
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}