using DurableAgent.Functions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Workflows;

public static class OrderProcessingWorkflow
{
    public const string WorkflowName = "order-processing-workflow";

    public static void RegisterWorkflow(FunctionsApplicationBuilder builder)
    {
        var orderIntakeAgent = builder.Services.BuildServiceProvider()
            .GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName);
        var fulfillmentDecisionAgent = builder.Services.BuildServiceProvider()
            .GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName);
        var customerMessagingAgent = builder.Services.BuildServiceProvider()
            .GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName);
        
        builder.AddWorkflow(WorkflowName, (sp, key) =>
        {
            var agents = new List<AIAgent>()
            {
                orderIntakeAgent,
                fulfillmentDecisionAgent,
                customerMessagingAgent
            };

            return AgentWorkflowBuilder.BuildSequential(
                workflowName: key,
                agents: agents);
                
        }).AddAsAIAgent();
    }
}