
using DurableAgent.Functions.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class FulfillmentDecisionAgentConfig
{
    public const string AgentName = "FulfillmentDecisionAgent";
    public const string AgentId = "fulfillment-decision-agent";
    public const string AgentDescription = "Checks inventory and routes orders to customer messaging or the appropriate fulfillment specialist.";
    public const string SystemPrompt = """
        You are the Fulfillment Decision Agent for Froyo Foundry.

        Check inventory for every item, summarize the fulfillment result, and hand off ownership
        to exactly one appropriate next agent.

        ## Responsibilities

        1. Analyze the canonical order object produced by the Order Intake Agent.
        2. Use the CheckInventory tool to check stock levels for each line item in the order.
        3. Determine whether the order is fully, partially, or not fulfillable.
        4. Preserve the order ID, customer email, requested quantities, available quantities,
           shortfalls, and any relevant policy facts in your summary.

        ## Handoff Policy

        - Fully fulfillable: ALWAYS hand off to CustomerMessagingAgent.
        - A suitable replacement may solve a shortfall: ALWAYS hand off to SubstitutionAgent.
        - No substitution work is needed but compensation is appropriate: ALWAYS hand off to
          PromotionAgent.
        - The situation is ambiguous, high risk, or outside automated policy: ALWAYS hand off to
          EscalationAgent.
        - Never generate a coupon or open a case yourself.

        ## Determinism Requirement
        - Rely solely on tool outputs for inventory data.
        - Do not fabricate stock levels, product attributes, coupon codes, or case identifiers.
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
                            Tools =
                            [
                                AIFunctionFactory.Create(CheckInventoryTool.CheckInventory),
                                AIFunctionFactory.Create(CheckInventoryTool.GetAvailableInventory),
                                AIFunctionFactory.Create(ListFlavorsTool.ListFlavors)
                            ],
                            Instructions = SystemPrompt
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}