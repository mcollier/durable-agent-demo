using DurableAgent.Functions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Workflows;

public static class OrderProcessingWorkflow
{
    public const string WorkflowName = "order-processing-workflow";

    public static void RegisterWorkflow(FunctionsApplicationBuilder builder)
    {
        builder.AddWorkflow(WorkflowName, (sp, key) =>
        {
            var agents = new List<AIAgent>()
            {
                sp.GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName),
                sp.GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName),
                sp.GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName)
            };

            return AgentWorkflowBuilder.BuildSequential(
                workflowName: key,
                agents: agents);

        }).AddAsAIAgent();
    }
}