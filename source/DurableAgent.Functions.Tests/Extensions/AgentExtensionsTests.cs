using DurableAgent.Functions.Extensions;
using DurableAgent.Functions.Workflows;
using FakeItEasy;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace DurableAgent.Functions.Tests.Extensions;

public class AgentExtensionsTests
{
    [Fact]
    public void WhenWorkflowAgentsAreRegistered_ThenDurableExecutorAliasesAreAvailable()
    {
        AIAgent orderIntake = CreateAgent("OrderIntakeAgent", "order-intake-agent");
        AIAgent fulfillment = CreateAgent("FulfillmentAgent", "fulfillment-agent");
        AIAgent customerMessaging = CreateAgent(
            "CustomerMessagingAgent",
            "customer-messaging-agent");
        Workflow workflow = OrderWorkflowFactory.Create(
            orderIntake,
            fulfillment,
            customerMessaging);
        IReadOnlyList<(string ExecutorId, AIAgent Agent)> aliases =
            AgentExtensions.ResolveWorkflowAgentAliases(
            workflow,
            [orderIntake, fulfillment, customerMessaging]);

        Assert.Equal(
            [
                "OrderIntakeAgent_order_intake_agent",
                "FulfillmentAgent_fulfillment_agent",
                "CustomerMessagingAgent_customer_messaging_agent"
            ],
            aliases.Select(alias => alias.ExecutorId));
        Assert.Equal(
            [orderIntake, fulfillment, customerMessaging],
            aliases.Select(alias => alias.Agent));
    }

    private static AIAgent CreateAgent(string name, string id) =>
        A.Fake<IChatClient>().AsAIAgent(
            new ChatClientAgentOptions
            {
                Id = id,
                Name = name,
                Description = $"{name} test agent"
            });
}
