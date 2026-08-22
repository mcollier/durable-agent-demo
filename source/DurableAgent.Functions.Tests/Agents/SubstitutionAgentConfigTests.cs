using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class SubstitutionAgentConfigTests
{
    [Fact]
    public void AgentIdentity_UsesDurableRegistryName()
    {
        Assert.Equal("SubstitutionAgent", SubstitutionAgentConfig.AgentName);
        Assert.False(string.IsNullOrWhiteSpace(SubstitutionAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_FindsSubstituteAndAlwaysIssuesOneCoupon()
    {
        Assert.Contains("inventory", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("flavor", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("25%", SubstitutionAgentConfig.SystemPrompt);
        Assert.Contains("no suitable substitute", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one coupon", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valid JSON", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handoff", SubstitutionAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
