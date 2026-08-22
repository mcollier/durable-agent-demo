using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class FulfillmentDecisionAgentConfigTests
{
    [Fact]
    public void AgentIdentity_UsesDurableRegistryName()
    {
        Assert.Equal("FulfillmentDecisionAgent", FulfillmentDecisionAgentConfig.AgentName);
        Assert.False(string.IsNullOrWhiteSpace(FulfillmentDecisionAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_OnlyDeterminesFulfillment()
    {
        Assert.Contains("canFullyFulfill", FulfillmentDecisionAgentConfig.SystemPrompt);
        Assert.Contains("valid JSON", FulfillmentDecisionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handoff", FulfillmentDecisionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PromotionAgent", FulfillmentDecisionAgentConfig.SystemPrompt);
        Assert.DoesNotContain("EscalationAgent", FulfillmentDecisionAgentConfig.SystemPrompt);
        Assert.DoesNotContain("generate a coupon", FulfillmentDecisionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
