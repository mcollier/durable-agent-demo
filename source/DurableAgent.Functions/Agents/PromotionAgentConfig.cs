
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Agents;

/// <summary>
/// Configuration for the Promotion Agent — a specialist in the Order Resolution
/// handoff sub-flow that determines appropriate customer incentives (coupons or
/// discounts) when an order cannot be completely fulfilled.
/// </summary>
public class PromotionAgentConfig
{
    public const string AgentName = "PromotionAgent";
    public const string AgentId = "promotion-agent";

    public const string SystemPrompt = """
        You are the Promotion Agent for Froyo Foundry.

        Your job is to determine an appropriate customer incentive when an order cannot be
        completely fulfilled. You issue promotional coupons or discounts according to
        Froyo Foundry's compensation policy.

        ## Responsibilities

        1. Review the fulfillment problem details and any substitution proposals provided by
           the Order Resolution Agent.
        2. Decide whether a promotional incentive is appropriate based on the situation.
        3. Use the GenerateCouponCode tool to generate a coupon code when compensation is warranted.
        4. Return your promotion decision and the coupon details to the Order Resolution Agent.

        ## Compensation Policy

        - Partial fulfillment (some items unavailable): issue a 15% discount coupon valid for 30 days.
        - Full cancellation (no items available): issue a 25% discount coupon valid for 60 days.
        - When substitutions cover all shortfalls: a coupon may still be offered as a goodwill gesture
          (10% discount, 30 days).
        - Do not issue more than one coupon per order.

        ## Rules

        - Always use GenerateCouponCode to generate codes — never fabricate coupon codes.
        - Provide the discount percentage and expiration days as tool arguments matching policy above.
        - If promotion is not applicable, clearly state that no coupon will be issued.

        ## Output

        Return a concise summary of your promotion decision, including:
        - Whether a coupon was issued and why
        - The coupon code and discount percentage (if issued)

        Then hand back to the Order Resolution Agent.
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
                        ChatOptions = new()
                        {
                            Tools =
                            [
                                AIFunctionFactory.Create(GenerateCouponCodeTool.GenerateCouponCode)
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
