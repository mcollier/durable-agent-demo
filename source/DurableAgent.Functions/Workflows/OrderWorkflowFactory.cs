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

        AIAgentHostOptions agentHostOptions = new()
        {
            ForwardIncomingMessages = false,
            ReassignOtherAgentsAsUsers = true
        };
        ExecutorBinding orderIntake = orderIntakeAgent.BindAsExecutor(agentHostOptions);
        ExecutorBinding fulfillment = fulfillmentAgent.BindAsExecutor(agentHostOptions);
        ExecutorBinding customerMessaging = customerMessagingAgent.BindAsExecutor(agentHostOptions);

        AgentTurnForwarderExecutor fulfillmentTurn = new("ForwardToFulfillment");
        AgentTurnForwarderExecutor customerMessagingTurn = new("ForwardToCustomerMessaging");
        OrderWorkflowStartExecutor start = new();
        CustomerMessageOutputExecutor output = new();

        return new WorkflowBuilder(start)
            .WithName(WorkflowName)
            .WithDescription("Validates orders, fulfills them, and notifies customers")
            .AddEdge(start, orderIntake)
            .AddEdge(orderIntake, fulfillmentTurn)
            .AddEdge(fulfillmentTurn, fulfillment)
            .AddEdge(fulfillment, customerMessagingTurn)
            .AddEdge(customerMessagingTurn, customerMessaging)
            .AddEdge(customerMessaging, output)
            .WithOutputFrom(output)
            .Build();
    }
}
