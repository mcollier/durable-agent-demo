using System.Text.Json;
using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace DurableAgent.Functions.Workflows;

internal static class OrderWorkflowFactory
{
    internal const string WorkflowName = "order-processing-workflow";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static Workflow Create(
        AIAgent orderIntakeAgent,
        AIAgent fulfillmentDecisionAgent,
        AIAgent substitutionAgent,
        AIAgent customerMessagingAgent)
    {
        ArgumentNullException.ThrowIfNull(orderIntakeAgent);
        ArgumentNullException.ThrowIfNull(fulfillmentDecisionAgent);
        ArgumentNullException.ThrowIfNull(substitutionAgent);
        ArgumentNullException.ThrowIfNull(customerMessagingAgent);

        AIAgentHostOptions agentHostOptions = new()
        {
            ForwardIncomingMessages = false,
            ReassignOtherAgentsAsUsers = true
        };
        ExecutorBinding orderIntake = orderIntakeAgent.BindAsExecutor(agentHostOptions);
        ExecutorBinding fulfillmentDecision = fulfillmentDecisionAgent.BindAsExecutor(agentHostOptions);
        ExecutorBinding substitution = substitutionAgent.BindAsExecutor(agentHostOptions);
        ExecutorBinding customerMessaging = customerMessagingAgent.BindAsExecutor(agentHostOptions);

        AgentTurnForwarderExecutor fulfillmentTurn = new("ForwardToFulfillment");
        AgentTurnForwarderExecutor substitutionTurn = new("ForwardToSubstitution");
        AgentTurnForwarderExecutor customerMessagingTurn = new("ForwardToCustomerMessaging");
        OrderWorkflowStartExecutor start = new();
        CustomerMessageOutputExecutor output = new();

        return new WorkflowBuilder(start)
            .WithName(WorkflowName)
            .WithDescription("Validates orders, checks fulfillment, resolves shortfalls, and notifies customers")
            .AddEdge(start, orderIntake)
            .AddSwitch(orderIntake, switchBuilder => switchBuilder
                .AddCase<List<ChatMessage>>(
                    message => HasResponse(message) && IsValidOrder(message),
                    fulfillmentTurn)
                .AddCase<List<ChatMessage>>(
                    message => HasResponse(message) && !IsValidOrder(message),
                    customerMessagingTurn))
            .AddEdge(fulfillmentTurn, fulfillmentDecision)
            .AddSwitch(fulfillmentDecision, switchBuilder => switchBuilder
                .AddCase<List<ChatMessage>>(
                    message => HasResponse(message) && CanFullyFulfill(message),
                    customerMessagingTurn)
                .AddCase<List<ChatMessage>>(
                    message => HasResponse(message) && !CanFullyFulfill(message),
                    substitutionTurn))
            .AddEdge(substitutionTurn, substitution)
            .AddEdge(substitution, customerMessagingTurn)
            .AddEdge(customerMessagingTurn, customerMessaging)
            .AddEdge(customerMessaging, output)
            .WithOutputFrom(output)
            .Build();
    }

    internal static bool IsValidOrder(object? message) =>
        DeserializeResult<OrderIntakeResult>(message).IsValid;

    internal static bool CanFullyFulfill(object? message) =>
        DeserializeResult<FulfillmentDecisionResult>(message).CanFullyFulfill;

    private static bool HasResponse(IEnumerable<ChatMessage>? messages) =>
        messages?.Any(message => !string.IsNullOrWhiteSpace(message.Text)) == true;

    private static TResult DeserializeResult<TResult>(object? message)
    {
        string text = message switch
        {
            string value => value,
            ChatMessage value when !string.IsNullOrWhiteSpace(value.Text) => value.Text,
            AgentResponse value when !string.IsNullOrWhiteSpace(value.Text) => value.Text,
            IEnumerable<ChatMessage> values => values.LastOrDefault(
                value => !string.IsNullOrWhiteSpace(value.Text))?.Text
                    ?? throw CreateRoutingException<TResult>(),
            _ => throw CreateRoutingException<TResult>()
        };

        try
        {
            return JsonSerializer.Deserialize<TResult>(text, JsonOptions)
                ?? throw CreateRoutingException<TResult>();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Workflow routing expected a valid {typeof(TResult).Name} JSON response.",
                exception);
        }
    }

    private static InvalidOperationException CreateRoutingException<TResult>() =>
        new($"Workflow routing expected a non-empty {typeof(TResult).Name} response.");
}
