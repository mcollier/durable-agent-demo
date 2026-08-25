using DurableAgent.Functions.Models;
using DurableAgent.Functions.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class FulfillmentAgentConfig
{
    public const string AgentName = "FulfillmentAgent";
    public const string AgentDescription = "Validates fulfillment, resolves shortfalls, and issues policy-approved coupons.";
    public const string SystemPrompt = """
        You are the Fulfillment Agent for Froyo Foundry.

        The input is an OrderIntakeResult. Return valid JSON matching OrderFulfillmentResult.

        ## Invalid Order

        If isValid is false:
        - Do not call any tools.
        - Set isValidOrder to false and preserve errorMessage in validationError.
        - Leave order identifiers null when unavailable, items and alternativeRecommendations empty,
          canFullyFulfill false, and coupon null.

        ## Valid Order

        1. Call CheckInventory for every line item using its canonical FlavorId.
        2. Record requested, available, fulfillable, and shortfall quantities.
        3. Set canFullyFulfill to true only when every original item has zero shortfall.
        4. When every shortfall is zero, do not call substitution or coupon tools; return no
           alternatives and a null coupon.
        5. When any item has a shortfall:
           - Call GetAvailableInventory and ListFlavors.
           - Select the closest suitable substitute only from products confirmed in stock.
           - Never recommend the unavailable original item.
           - If no suitable substitute exists, leave alternativeRecommendations empty.
           - Choose a coupon tier of 10%, 15%, 20%, or 25% based on shortfall severity:
             - 10% or 15% for a minor shortfall with a suitable substitute.
             - 20% for a substantial shortfall.
             - 25% when no suitable substitute exists or the order is largely unavailable.
           - Call GenerateCouponCode exactly once with the selected tier and expirationDays 30.
           - Copy the generated code and selected tier into coupon.

        ## Output Rules

        - Set isValidOrder to true for valid orders.
        - Populate SKU values in {FlavorId}-TUB format.
        - fulfillableQty is min(requestedQty, availableQty).
        - shortfallQty is requestedQty minus fulfillableQty.
        - Never fabricate inventory, substitutions, or coupon codes.
        - Return JSON only. Do not discuss internal tools, routing, workflows, or other agents.
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
                        Name = key,
                        Description = AgentDescription,
                        ChatOptions = new()
                        {
                            Tools =
                            [
                                AIFunctionFactory.Create(CheckInventoryTool.CheckInventory),
                                AIFunctionFactory.Create(CheckInventoryTool.GetAvailableInventory),
                                AIFunctionFactory.Create(ListFlavorsTool.ListFlavors),
                                AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode)
                            ],
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(OrderFulfillmentResult)),
                                schemaName: nameof(OrderFulfillmentResult),
                                schemaDescription: "The validated fulfillment outcome, substitutions, and coupon.")
                        }
                    },
                    chatClient: chatClient);
            });
    }
}
