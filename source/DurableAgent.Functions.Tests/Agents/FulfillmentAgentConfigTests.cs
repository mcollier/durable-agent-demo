using DurableAgent.Functions.Agents;
using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Tests.Agents;

public class FulfillmentAgentConfigTests
{
    [Fact]
    public void AgentIdentity_ReflectsExpandedResponsibility()
    {
        Assert.Equal("FulfillmentAgent", FulfillmentAgentConfig.AgentName);
        Assert.False(string.IsNullOrWhiteSpace(FulfillmentAgentConfig.AgentDescription));
    }

    [Fact]
    public void SystemPrompt_HandlesInvalidOrdersWithoutTools()
    {
        Assert.Contains("invalid order", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not call", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_GroundsFulfillmentInTools()
    {
        Assert.Contains("CheckInventory", FulfillmentAgentConfig.SystemPrompt);
        Assert.Contains("GetAvailableInventory", FulfillmentAgentConfig.SystemPrompt);
        Assert.Contains("ListFlavors", FulfillmentAgentConfig.SystemPrompt);
        Assert.Contains("GenerateCouponCode", FulfillmentAgentConfig.SystemPrompt);
        Assert.Contains("in stock", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly once", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_BoundsCouponDecision()
    {
        Assert.Contains("10%, 15%, 20%, or 25%", FulfillmentAgentConfig.SystemPrompt);
        Assert.Contains("shortfall", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no suitable substitute", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_ProducesUnifiedStructuredResult()
    {
        Assert.Contains(nameof(OrderFulfillmentResult), FulfillmentAgentConfig.SystemPrompt);
        Assert.Contains("valid JSON", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("later step", FulfillmentAgentConfig.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }
}
