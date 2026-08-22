using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class OrderIntakeAgentConfigTests
{
    [Fact]
    public void AgentIdentity_IsStable()
    {
        Assert.Equal("OrderIntakeAgent", OrderIntakeAgentConfig.AgentName);
        Assert.Equal("order-intake-agent", OrderIntakeAgentConfig.AgentId);
        Assert.False(string.IsNullOrWhiteSpace(OrderIntakeAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_RoutesValidAndInvalidOrders()
    {
        Assert.Contains("FulfillmentDecisionAgent", OrderIntakeAgentConfig.SystemPrompt);
        Assert.Contains("CustomerMessagingAgent", OrderIntakeAgentConfig.SystemPrompt);
        Assert.Contains("valid", OrderIntakeAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid", OrderIntakeAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
