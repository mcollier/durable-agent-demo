using DurableAgent.Functions.Agents;

namespace DurableAgent.Functions.Tests.Agents;

public class OrderIntakeAgentConfigTests
{
    [Fact]
    public void AgentIdentity_UsesDurableRegistryName()
    {
        Assert.Equal("OrderIntakeAgent", OrderIntakeAgentConfig.AgentName);
        Assert.False(string.IsNullOrWhiteSpace(OrderIntakeAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_ProducesStructuredValidationResultWithoutHandoffs()
    {
        Assert.Contains("isValid", OrderIntakeAgentConfig.SystemPrompt);
        Assert.Contains("errorMessage", OrderIntakeAgentConfig.SystemPrompt);
        Assert.Contains("valid JSON", OrderIntakeAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handoff", OrderIntakeAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CustomerMessagingAgent", OrderIntakeAgentConfig.SystemPrompt);
    }
}
