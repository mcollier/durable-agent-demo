using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class FulfillmentDecisionAgentConfigTests
{
    [Fact]
    public void AgentIdentity_IsStable()
    {
        Assert.Equal("FulfillmentDecisionAgent", FulfillmentDecisionAgentConfig.AgentName);
        Assert.Equal("fulfillment-decision-agent", FulfillmentDecisionAgentConfig.AgentId);
        Assert.False(string.IsNullOrWhiteSpace(FulfillmentDecisionAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_DescribesEveryAllowedHandoff()
    {
        Assert.Contains("CustomerMessagingAgent", FulfillmentDecisionAgentConfig.SystemPrompt);
        Assert.Contains("SubstitutionAgent", FulfillmentDecisionAgentConfig.SystemPrompt);
        Assert.Contains("PromotionAgent", FulfillmentDecisionAgentConfig.SystemPrompt);
        Assert.Contains("EscalationAgent", FulfillmentDecisionAgentConfig.SystemPrompt);
    }
}
