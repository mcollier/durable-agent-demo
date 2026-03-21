using DurableAgent.Functions.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;

namespace DurableAgent.Functions.Extensions;

public static class WorkflowServiceExtensions
{
    public static FunctionsApplicationBuilder RegisterWorkflow(this FunctionsApplicationBuilder builder)
    {
        OrderProcessingWorkflow.RegisterWorkflow(builder);
        
        return builder;
    }
}