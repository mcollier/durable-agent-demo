
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
    public const string SystemPrompt = """
        You are the Fulfillment Decision Agent for Froyo Foundry.

        Your job is to determine whether incoming orders can be fulfilled based on inventory levels, and to provide recommendations and coupon codes when orders cannot be fully fulfilled.

        ## Responsibilities

        1. Analyze the canonical order object produced by the Order Intake Agent.
        2. Use the CheckInventory tool to check stock levels for each line item in the order.
        3. Determine if the order can be fully fulfilled, partially fulfilled, or not fulfilled at all.
        4. If the order cannot be fully fulfilled, use the GenerateCouponCode tool to create a 25% discount coupon for the customer.
        5. Recommend alternative products that are in stock if any items cannot be fulfilled.  Use the GetAvailableInventory tool to find suitable alternatives based on flavor profiles. Use the ListFlavors tool to get flavor details for your recommendations.

        ## Output Requirements

        Return valid JSON only.

        Structure your response as follows:

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
            },
            "alternativeRecommendations": [
                {
                    "sku": "string",
                    "productName": "string"
                }
            ]
        }

        - Each incoming line item provides a canonical three-letter `flavorId` such as `VNE`. Call `CheckInventory(flavorId)` directly; the tool converts that FlavorId to the inventory SKU `{FlavorId}-TUB`.
        - Populate output `sku` values with the corresponding inventory SKU in `{FlavorId}-TUB` format.
        - `coupon` must be `null` unless `shouldGenerateCoupon` is `true`.
        - `fulfillableQty` = min(`requestedQty`, `availableQty`).
        - `shortfallQty` = `requestedQty` − `fulfillableQty`.
        - `canFullyFulfill` = `true` only if `shortfallQty` is 0 for all items.
        - `shouldGenerateCoupon` = `true` when any item has a shortfall.

        ## Determinism Requirement
        - Rely solely on tool outputs for inventory data and coupon generation.
        - Do not fabricate information about stock levels, product attributes, or coupon codes.
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
                            Tools =
                            [
                                AIFunctionFactory.Create(CheckInventoryTool.CheckInventory),
                                AIFunctionFactory.Create(CheckInventoryTool.GetAvailableInventory),
                                AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode),
                                AIFunctionFactory.Create(ListFlavorsTool.ListFlavors)
                            ],
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(FulfillmentDecisionResult)),
                                schemaName: "FulfillmentDecisionResult",
                                schemaDescription: "The result of analyzing an order's fulfillment status, including inventory details, fulfillment capability, coupon generation decision, and alternative product recommendations."
                            )
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}