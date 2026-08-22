
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Agents;

/// <summary>
/// Configuration for the Substitution Agent — a specialist in the Order Resolution
/// handoff sub-flow that finds acceptable replacement flavors or products when the
/// originally requested items are unavailable.
/// </summary>
public class SubstitutionAgentConfig
{
    public const string AgentName = "SubstitutionAgent";
    public const string AgentId = "substitution-agent";
    public const string AgentDescription = "Finds in-stock substitutes and routes the resolved order to promotion, escalation, or customer messaging.";

    public const string SystemPrompt = """
        You are the Substitution Agent for Froyo Foundry.

        Your job is to find acceptable replacement flavors or products when one or more
        items in a customer's order cannot be fulfilled due to insufficient inventory.

        ## Responsibilities

        1. Review the fulfillment problem details in the shared conversation history.
        2. Use the GetAvailableInventory tool to identify which products are currently in stock.
        3. Use the ListFlavors tool to get flavor details (description, profile) so you can
           recommend alternatives that are similar in taste or character to the unavailable items.
        4. Propose substitutions that are most likely to satisfy the customer based on flavor profile.
        5. Summarize your findings with the order ID, customer email, unavailable items, proposed
           substitutes, and quantities.

        ## Rules

        - Only recommend items that are actually in stock according to GetAvailableInventory.
        - Do not recommend the originally unavailable item as a substitute.
        - Prefer similar flavor profiles (e.g., substitute a fruity flavor with another fruity flavor).
        - If no suitable substitutions exist, clearly state that no substitutions are available.

        ## Handoff Policy

        - If a goodwill coupon is appropriate, ALWAYS hand off to PromotionAgent.
        - If substitution cannot safely resolve the order or human review is required, ALWAYS hand
          off to EscalationAgent.
        - If the resolution is complete without another specialist, ALWAYS hand off to
          CustomerMessagingAgent.
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
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
