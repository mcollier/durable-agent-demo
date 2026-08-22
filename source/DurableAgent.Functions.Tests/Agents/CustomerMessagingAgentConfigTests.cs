using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class CustomerMessagingAgentConfigTests
{
    [Fact]
    public void AgentIdentity_IsStable()
    {
        Assert.Equal("CustomerMessagingAgent", CustomerMessagingAgentConfig.AgentName);
        Assert.Equal("customer-messaging-agent", CustomerMessagingAgentConfig.AgentId);
        Assert.False(string.IsNullOrWhiteSpace(CustomerMessagingAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_UsesConversationHistoryAndProducesTerminalOutput()
    {
        Assert.Contains("conversation history", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("substitution", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coupon", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("escalation", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("final", CustomerMessagingAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
