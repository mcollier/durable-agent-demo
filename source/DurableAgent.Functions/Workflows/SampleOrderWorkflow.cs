using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;

namespace DurableAgent.Functions.Workflows;

public static class SampleOrderWorkflow
{
    public const string WorkflowName = "sample-order-workflow";

    public static void RegisterWorkflow(FunctionsApplicationBuilder builder)
    {
        // Define executors for all workflows
        OrderLookup orderLookup = new();
        OrderCancel orderCancel = new();
        SendEmail sendEmail = new();
        // StatusReport statusReport = new();
        // BatchCancelProcessor batchCancelProcessor = new();
        // BatchCancelSummary batchCancelSummary = new();

        // Build the CancelOrder workflow: OrderLookup -> OrderCancel -> SendEmail
        Workflow cancelOrder = new WorkflowBuilder(orderLookup)
            .WithName("CancelOrder")
            .WithDescription("Cancel an order and notify the customer")
            .AddEdge(orderLookup, orderCancel)
            .AddEdge(orderCancel, sendEmail)
            .Build();

        builder.ConfigureDurableWorkflows(workflows => workflows.AddWorkflows(cancelOrder));
    }
}
