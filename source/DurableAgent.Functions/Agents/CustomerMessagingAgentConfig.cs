
using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class CustomerMessagingAgentConfig
{
    public const string AgentName = "CustomerMessagingAgent";
    public const string SystemPrompt = """
        You are the Customer Messaging Agent for Froyo Foundry.

        Your job is to craft clear and empathetic messages to customers about their order status based on the fulfillment analysis provided by the Fulfillment Decision Agent.

        ## Responsibilities

        1. Read the fulfillment decision output, including inventory details, fulfillment capability, coupon information, and alternative product recommendations.
        2. Determine the appropriate messaging scenario (full fulfillment, partial fulfillment, no fulfillment).
        3. Craft a clear, concise, and empathetic message to the customer regarding their order status.
        4. Include information about any coupons or alternative products if applicable.

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

        Messages must be:
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
            "message": "string"
        }
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        // Get the IChatClient from the DI container
        var chatClient = builder.Services.BuildServiceProvider().GetRequiredService<IChatClient>();

        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                AIAgent agent = new ChatClientAgent(
                    options: new ChatClientAgentOptions
                    {
                        Name = key,
                        ChatOptions = new()
                        {
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(CustomerMessageResult)),
                                schemaName: "CustomerMessageResult",
                                schemaDescription: "A message to a customer regarding their order fulfillment status, containing the order ID and the message body."
                            )
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}