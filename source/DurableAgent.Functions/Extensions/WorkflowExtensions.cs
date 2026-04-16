using DurableAgent.Functions.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;

namespace DurableAgent.Functions.Extensions;

[Obsolete("WorkflowServiceExtensions is not currently used and may be removed in a future release.")]
public static class WorkflowServiceExtensions
{
    [Obsolete("RegisterWorkflow is not currently used and may be removed in a future release.")]
    public static FunctionsApplicationBuilder RegisterWorkflow(this FunctionsApplicationBuilder builder)
    {
        OrderProcessingWorkflow.RegisterWorkflow(builder);

        SampleOrderWorkflow.RegisterWorkflow(builder);
        
        return builder;
    }
}