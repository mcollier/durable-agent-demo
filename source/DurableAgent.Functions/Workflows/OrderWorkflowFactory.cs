using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace DurableAgent.Functions.Workflows;

internal static class OrderWorkflowFactory
{
    internal const string WorkflowName = "order-processing-workflow";

    internal static Workflow Create(
        AIAgent orderIntakeAgent,
        AIAgent fulfillmentAgent,
        AIAgent customerMessagingAgent)
    {
        ArgumentNullException.ThrowIfNull(orderIntakeAgent);
        ArgumentNullException.ThrowIfNull(fulfillmentAgent);
        ArgumentNullException.ThrowIfNull(customerMessagingAgent);

        return new WorkflowBuilder(orderIntakeAgent)
            .WithName(WorkflowName)
            .WithDescription("Validates orders, fulfills them, and notifies customers")
            .AddEdge(orderIntakeAgent, fulfillmentAgent)
            .AddEdge(fulfillmentAgent, customerMessagingAgent)
            .WithOutputFrom(customerMessagingAgent)
            .Build();
    }
}
