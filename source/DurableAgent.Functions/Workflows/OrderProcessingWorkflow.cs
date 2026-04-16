using DurableAgent.Functions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Workflows;

[Obsolete("OrderProcessingWorkflow is not currently used and may be removed in a future release.")]
public static class OrderProcessingWorkflow
{
    public const string WorkflowName = "order-processing-workflow";

    public static void RegisterWorkflow(FunctionsApplicationBuilder builder)
    {
        // builder.AddWorkflow(WorkflowName, (sp, key) =>
        // {
        //     var agents = new List<AIAgent>()
        //     {
        //         sp.GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName),
        //         sp.GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName),
        //         sp.GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName)
        //     };

        //     // TODO: Add the workflow to the Durable Functions when updates published to NuGet.
        //     // builder.ConfigureDurableWorkflows(workflows => workflows.AddWorkflows(workflow));

        //     return AgentWorkflowBuilder.BuildSequential(
        //         workflowName: key,
        //         agents: agents);

        // }).AddAsAIAgent();

        // Regular workflow - not agent
        // builder.AddWorkflow(WorkflowName, (sp, key) =>
        // {
        //     var agents = new List<AIAgent>()
        //     {
        //         sp.GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName),
        //         sp.GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName),
        //         sp.GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName)
        //     };

        //     // TODO: Add the workflow to the Durable Functions when updates published to NuGet.
        //     // builder.ConfigureDurableWorkflows(workflows => workflows.AddWorkflows(workflow));

        //     return AgentWorkflowBuilder.BuildSequential(
        //         workflowName: key,
        //         agents: agents);

        // });

        var orderIntakeAgent = builder.Services.BuildServiceProvider()
            .GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName);
        var fulfillmentDecisionAgent = builder.Services.BuildServiceProvider()
            .GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName);
        var customerMessagingAgent = builder.Services.BuildServiceProvider()
            .GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName);

        Workflow orderProcessingWorkflow = new WorkflowBuilder(orderIntakeAgent)
                                            .WithName(WorkflowName)
                                            .WithDescription("Workflow to process customer orders")
                                            .AddEdge(orderIntakeAgent, fulfillmentDecisionAgent)
                                            .AddEdge(fulfillmentDecisionAgent, customerMessagingAgent)
                                            .Build();
        // var workflow = AgentWorkflowBuilder.BuildSequential(
        //     workflowName: WorkflowName,
        //     agents:
        //     [
        //         orderIntakeAgent,
        //         fulfillmentDecisionAgent,
        //         customerMessagingAgent
        //     ]
        // );

        builder.ConfigureDurableWorkflows(workflows => workflows.AddWorkflows(orderProcessingWorkflow));
    }
}