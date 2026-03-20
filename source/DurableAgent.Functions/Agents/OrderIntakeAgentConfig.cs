
using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class OrderIntakeAgentConfig
{
    public const string AgentName = "OrderIntakeAgent";
    public const string SystemPrompt = """
        You are the Order Intake Agent for Froyo Foundry.

        Your job is to process incoming orders, validate them against business rules, and produce a structured output that can be used by downstream agents in the order processing workflow.

        ## Responsibilities

        1. Validate incoming order data for required fields and correct formatting.
        2. Check orders against business rules (e.g., max quantity limits, restricted products).
        3. Produce a canonical order object that includes all necessary information for fulfillment.
        4. If the order violates any rules, produce a clear and specific error message indicating the issue.

        ## Business Rules

        - Maximum quantity per item is 10.
        - Minimum quantity per item is 1.
        - Restricted products include "Rainbow Sherbet" and "Chocolate Chip Cookie Dough".
        - Orders must include customer name, email, shipping address, and at least one line item with FlavorId and quantity.

        ## Output Requirements

        Return valid JSON only.

        For valid orders, structure your response as follows:

        {
            "isValid": true,
            "order": {
                "orderId": "string",
                "customerName": {
                    "firstName": "string",
                    "middleName": "string or null",
                    "lastName": "string"
                },
                "customerEmail": "string",
                "shippingAddress": {
                    "streetAddress": "string",
                    "addressLine2": "string or null",
                    "city": "string",
                    "state": "string",
                    "zipCode": "string"
                },
                "lineItems": [
                    {
                        "flavorId": "string",
                        "quantity": 0
                    }
                ]
            },
            "errorMessage": null
        }

        For invalid orders, structure your response as follows:

        {
            "isValid": false,
            "order": null,
            "errorMessage": "string describing the validation error"
        }
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {

        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                // Get the IChatClient from the DI container
                var chatClient = sp.GetRequiredService<IChatClient>();

                AIAgent agent = new ChatClientAgent(
                    options: new ChatClientAgentOptions
                    {
                        Name = key,
                        ChatOptions = new()
                        {
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(OrderIntakeResult)),
                                schemaName: "OrderIntakeResult",
                                schemaDescription: "The result of validating and processing an incoming order, including a canonical order object if valid or an error message if invalid."
                            )
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}