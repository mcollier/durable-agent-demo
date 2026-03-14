

using Microsoft.Agents.AI.Workflows;

internal sealed partial class PackageResultsExecutor() : Executor("PackageResultsExecutor")
{
    protected override RouteBuilder ConfigureRoutes(RouteBuilder routeBuilder)
    {
        throw new NotImplementedException();
    }

    [MessageHandler]
    private ValueTask<string> HandleAsync(string message, IWorkflowContext context)
    {
        return new ValueTask<string>(message);
    }
    
}