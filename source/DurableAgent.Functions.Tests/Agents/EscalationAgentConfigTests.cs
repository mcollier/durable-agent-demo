using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class EscalationAgentConfigTests
{
    [Fact]
    public void AgentName_IsEscalationAgent()
    {
        Assert.Equal("EscalationAgent", EscalationAgentConfig.AgentName);
    }

    [Fact]
    public void AgentId_IsEscalationAgent()
    {
        Assert.Equal("escalation-agent", EscalationAgentConfig.AgentId);
    }

    [Fact]
    public void SystemPrompt_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(EscalationAgentConfig.SystemPrompt));
    }

    [Fact]
    public void SystemPrompt_MentionsHuman()
    {
        Assert.Contains("human", EscalationAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_MentionsEscalation()
    {
        Assert.Contains("escalat", EscalationAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
