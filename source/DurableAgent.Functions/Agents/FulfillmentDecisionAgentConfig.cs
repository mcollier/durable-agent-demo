using DurableAgent.Functions.Models;
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
    public const string AgentDescription = "Checks inventory and returns a structured fulfillment decision.";
    public const string SystemPrompt = """
        You are the Fulfillment Decision Agent for Froyo Foundry.

        Check inventory for every item in the canonical order and return valid JSON matching the
        required response schema.

        ## Responsibilities

        1. Call CheckInventory for each line item using its canonical FlavorId.
        2. Record requested, available, fulfillable, and shortfall quantities.
        3. Set canFullyFulfill to true only when every shortfall is zero.
        4. Set shouldGenerateCoupon to true when any item has a shortfall.
        5. Leave coupon null and alternativeRecommendations empty. A later step resolves shortfalls.

        ## Rules

        - Populate SKU values in {FlavorId}-TUB format.
        - fulfillableQty is min(requestedQty, availableQty).
        - shortfallQty is requestedQty minus fulfillableQty.
        - Rely only on CheckInventory output. Never fabricate stock, substitutes, or coupon codes.
        - Return JSON only. Do not discuss routing, workflows, or other agents.
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                var chatClient = sp.GetRequiredService<IChatClient>();

                return new ChatClientAgent(
                    options: new ChatClientAgentOptions
                    {
                        Id = AgentId,
                        Name = key,
                        Description = AgentDescription,
                        ChatOptions = new()
                        {
                            Tools =
                            [
                                AIFunctionFactory.Create(CheckInventoryTool.CheckInventory)
                            ],
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(FulfillmentDecisionResult)),
                                schemaName: nameof(FulfillmentDecisionResult),
                                schemaDescription: "The structured inventory and fulfillment decision.")
                        }
                    },
                    chatClient: chatClient);
            });
    }
}
