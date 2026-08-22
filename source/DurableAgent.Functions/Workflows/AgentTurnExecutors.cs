using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace DurableAgent.Functions.Workflows;

[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
internal sealed class OrderWorkflowStartExecutor()
    : Executor<string>(nameof(OrderWorkflowStartExecutor))
{
    public override async ValueTask HandleAsync(
        string orderJson,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, orderJson),
            cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}

[SendsMessage(typeof(List<ChatMessage>))]
[SendsMessage(typeof(TurnToken))]
internal sealed class AgentTurnForwarderExecutor(string id) : Executor<List<ChatMessage>>(id)
{
    public override async ValueTask HandleAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.SendMessageAsync(messages, cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}

internal sealed class CustomerMessageOutputExecutor()
    : Executor<List<ChatMessage>, string>(nameof(CustomerMessageOutputExecutor))
{
    public override ValueTask<string> HandleAsync(
        List<ChatMessage> messages,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string output = messages.LastOrDefault(message => message.Role == ChatRole.Assistant)?.Text
            ?? throw new InvalidOperationException(
                "Customer Messaging completed without an assistant response.");

        return ValueTask.FromResult(output);
    }
}
