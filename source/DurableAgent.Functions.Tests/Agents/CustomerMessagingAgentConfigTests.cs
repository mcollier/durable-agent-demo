using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class CustomerMessagingAgentConfigTests
{
    [Fact]
    public void AgentIdentity_UsesDurableRegistryName()
    {
        Assert.Equal("CustomerMessagingAgent", CustomerMessagingAgentConfig.AgentName);
        Assert.False(string.IsNullOrWhiteSpace(CustomerMessagingAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_HandlesEveryTerminalRoute()
    {
        Assert.Contains("OrderFulfillmentResult", CustomerMessagingAgentConfig.SystemPrompt);
        Assert.Contains("invalid order", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full fulfillment", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("substitute", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no substitute", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coupon", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("when orderId is unavailable", CustomerMessagingAgentConfig.SystemPrompt);
        Assert.DoesNotContain("handoff", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("escalation", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_GreetsCustomerByFirstAndLastName()
    {
        Assert.Contains("customerName", CustomerMessagingAgentConfig.SystemPrompt);
        Assert.Contains("{firstName} {lastName}", CustomerMessagingAgentConfig.SystemPrompt);
        Assert.Contains("Never invent", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
