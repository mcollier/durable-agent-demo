using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class PromotionAgentConfigTests
{
    [Fact]
    public void AgentName_IsPromotionAgent()
    {
        Assert.Equal("PromotionAgent", PromotionAgentConfig.AgentName);
    }

    [Fact]
    public void AgentId_IsPromotionAgent()
    {
        Assert.Equal("promotion-agent", PromotionAgentConfig.AgentId);
    }

    [Fact]
    public void SystemPrompt_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(PromotionAgentConfig.SystemPrompt));
    }

    [Fact]
    public void SystemPrompt_MentionsCoupon()
    {
        Assert.Contains("coupon", PromotionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_MentionsDiscount()
    {
        Assert.Contains("discount", PromotionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_PreventsDuplicateCouponsAndRoutesForward()
    {
        Assert.Contains("one coupon", PromotionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomerMessagingAgent", PromotionAgentConfig.SystemPrompt);
        Assert.Contains("EscalationAgent", PromotionAgentConfig.SystemPrompt);
        Assert.DoesNotContain("Order Resolution Agent", PromotionAgentConfig.SystemPrompt);
    }
}
