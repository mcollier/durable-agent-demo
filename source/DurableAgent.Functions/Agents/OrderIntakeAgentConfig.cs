
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class OrderIntakeAgentConfig
{
    public const string AgentName = "OrderIntakeAgent";
    public const string AgentId = "order-intake-agent";
    public const string AgentDescription = "Validates incoming orders and routes valid orders to fulfillment or invalid orders to customer messaging.";
    public const string SystemPrompt = """
        You are the Order Intake Agent for Froyo Foundry.

        Validate each incoming order, summarize the result clearly, and then hand off ownership.

        ## Handoff Policy

        - If the order is valid, summarize the canonical order and ALWAYS hand off to
          FulfillmentDecisionAgent.
        - If the order is invalid, summarize every validation failure and ALWAYS hand off to
          CustomerMessagingAgent. Do not send an invalid order to fulfillment.

        ## Business Rules

        - Maximum quantity per item is 10.
        - Minimum quantity per item is 1.
        - Restricted products include "Rainbow Sherbet" and "Chocolate Chip Cookie Dough".
        - Orders must include customer name, email, shipping address, and at least one line item with FlavorId and quantity.

        Preserve the order ID, customer email, shipping details, flavor IDs, quantities, and all
        validation findings in your summary so the receiving agent has the facts it needs.
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
                        Id = AgentId,
                        Name = key,
                        Description = AgentDescription,
                        ChatOptions = new()
                        {
                            Instructions = SystemPrompt
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}