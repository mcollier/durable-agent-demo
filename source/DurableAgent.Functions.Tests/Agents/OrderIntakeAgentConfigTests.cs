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

    [Fact]
    public void SystemPrompt_RequiresFlavorCatalogValidation()
    {
        Assert.Contains("ListFlavors", OrderIntakeAgentConfig.SystemPrompt);
        Assert.Contains("FlavorId", OrderIntakeAgentConfig.SystemPrompt);
        Assert.Contains(
            "not present in the catalog, the entire order is invalid",
            OrderIntakeAgentConfig.SystemPrompt);
    }

    [Fact]
    public void SystemPrompt_RestrictedProductsAreRealCatalogFlavorIds()
    {
        Assert.Contains("\"AIA\"", OrderIntakeAgentConfig.SystemPrompt);
        Assert.DoesNotContain("\"NPP\"", OrderIntakeAgentConfig.SystemPrompt);
        Assert.DoesNotContain("\"PBP\"", OrderIntakeAgentConfig.SystemPrompt);
        Assert.DoesNotContain("Rainbow Sherbet", OrderIntakeAgentConfig.SystemPrompt);
        Assert.DoesNotContain("Chocolate Chip Cookie Dough", OrderIntakeAgentConfig.SystemPrompt);
    }
}
