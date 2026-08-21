using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class OrderResolutionAgentConfigTests
{
    [Fact]
    public void AgentName_IsOrderResolutionAgent()
    {
        Assert.Equal("OrderResolutionAgent", OrderResolutionAgentConfig.AgentName);
    }

    [Fact]
    public void AgentId_IsOrderResolutionAgent()
    {
        Assert.Equal("order-resolution-agent", OrderResolutionAgentConfig.AgentId);
    }

    [Fact]
    public void SystemPrompt_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(OrderResolutionAgentConfig.SystemPrompt));
    }

    [Fact]
    public void SystemPrompt_MentionsSubstitution()
    {
        Assert.Contains("substitut", OrderResolutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_MentionsPromotion()
    {
        Assert.Contains("promot", OrderResolutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_MentionsEscalation()
    {
        Assert.Contains("escalat", OrderResolutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
