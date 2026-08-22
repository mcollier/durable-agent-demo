using System.Text.Json;
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

[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
internal sealed class AgentTurnForwarderExecutor(string id) : Executor<object>(id)
{
    public override async ValueTask HandleAsync(
        object message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (message is TurnToken)
        {
            return;
        }

        string text = AgentTurnMessage.GetText(message);
        await context.SendMessageAsync(
            new ChatMessage(ChatRole.User, text),
            cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken);
    }
}

internal sealed class CustomerMessageOutputExecutor()
    : Executor<object, string?>(nameof(CustomerMessageOutputExecutor))
{
    public override ValueTask<string?> HandleAsync(
        object message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            message is TurnToken ? null : AgentTurnMessage.GetText(message));
}

internal static class AgentTurnMessage
{
    internal static string GetText(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            IEnumerable<ChatMessage> messages => messages
                .LastOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.Text))?.Text
                    ?? throw CreateException(),
            JsonElement element => GetDurableInput(element),
            string[] envelopes => GetDurableInput(envelopes.LastOrDefault()),
            string text when !string.IsNullOrWhiteSpace(text) => text,
            _ => throw CreateException(message.GetType())
        };
    }

    private static string GetDurableInput(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
        {
            throw CreateException();
        }

        JsonElement lastEnvelope = element.EnumerateArray().Last();
        if (lastEnvelope.ValueKind != JsonValueKind.String)
        {
            throw CreateException();
        }

        return GetDurableInput(lastEnvelope.GetString());
    }

    private static string GetDurableInput(string? envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope))
        {
            throw CreateException();
        }

        using JsonDocument document = JsonDocument.Parse(envelope);
        if (!document.RootElement.TryGetProperty("input", out JsonElement input) ||
            input.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(input.GetString()))
        {
            throw CreateException();
        }

        return input.GetString()!;
    }

    private static InvalidOperationException CreateException() =>
        new("The durable agent completed without a routable response.");

    private static InvalidOperationException CreateException(Type messageType) =>
        new($"The durable agent produced an unsupported response type '{messageType.FullName}'.");
}
