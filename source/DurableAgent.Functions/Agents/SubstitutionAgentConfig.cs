using DurableAgent.Functions.Models;
using DurableAgent.Functions.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class SubstitutionAgentConfig
{
    public const string AgentName = "SubstitutionAgent";
    public const string AgentId = "substitution-agent";
    public const string AgentDescription = "Finds an in-stock substitute and issues a goodwill coupon for fulfillment shortfalls.";
    public const string SystemPrompt = """
        You are the Substitution Agent for Froyo Foundry.

        Resolve a fulfillment shortfall and return valid JSON matching the
        FulfillmentDecisionResult response schema.

        ## Responsibilities

        1. Use GetAvailableInventory to find products that are currently in stock.
        2. Use ListFlavors to compare flavor profiles.
        3. Recommend the closest in-stock flavor as an alternative when a suitable substitute exists.
        4. If no suitable substitute exists, leave alternativeRecommendations empty and preserve
           the original shortfall details.
        5. Always call GenerateCouponCode exactly once with discountPercent 25 (a 25% discount)
           and expirationDays 30.
        6. Include the returned code and a discountPercent of 25 in coupon.

        ## Rules

        - Recommend only products confirmed in stock.
        - Never recommend the unavailable original item.
        - Never fabricate inventory or coupon codes.
        - Set shouldGenerateCoupon to true and keep canFullyFulfill false.
        - A no suitable substitute result still requires one coupon.
        - Return JSON only. Do not discuss routing or workflows.
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
                                AIFunctionFactory.Create(CheckInventoryTool.GetAvailableInventory),
                                AIFunctionFactory.Create(ListFlavorsTool.ListFlavors),
                                AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode)
                            ],
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(FulfillmentDecisionResult)),
                                schemaName: nameof(FulfillmentDecisionResult),
                                schemaDescription: "The fulfillment result enriched with a substitute and coupon.")
                        }
                    },
                    chatClient: chatClient);
            });
    }
}
