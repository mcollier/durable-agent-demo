using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class SubstitutionAgentConfigTests
{
    [Fact]
    public void AgentName_IsSubstitutionAgent()
    {
        Assert.Equal("SubstitutionAgent", SubstitutionAgentConfig.AgentName);
    }

    [Fact]
    public void AgentId_IsSubstitutionAgent()
    {
        Assert.Equal("substitution-agent", SubstitutionAgentConfig.AgentId);
    }

    [Fact]
    public void SystemPrompt_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(SubstitutionAgentConfig.SystemPrompt));
    }

    [Fact]
    public void SystemPrompt_MentionsInventory()
    {
        Assert.Contains("inventory", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_MentionsFlavor()
    {
        Assert.Contains("flavor", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CustomerMessagingAgent")]
    [InlineData("PromotionAgent")]
    [InlineData("EscalationAgent")]
    public void SystemPrompt_MentionsEveryAllowedHandoff(string target)
    {
        Assert.Contains(target, SubstitutionAgentConfig.SystemPrompt);
    }
}
