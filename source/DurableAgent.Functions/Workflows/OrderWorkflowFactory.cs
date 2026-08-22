using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace DurableAgent.Functions.Workflows;

internal static class OrderWorkflowFactory
{
    internal const string WorkflowName = "order-processing-workflow";
    private const int AutonomousTurnLimit = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static Workflow Create(
        AIAgent orderIntakeAgent,
        AIAgent fulfillmentDecisionAgent,
        AIAgent substitutionAgent,
        AIAgent promotionAgent,
        AIAgent escalationAgent,
        AIAgent customerMessagingAgent)
    {
        ArgumentNullException.ThrowIfNull(orderIntakeAgent);
        ArgumentNullException.ThrowIfNull(fulfillmentDecisionAgent);
        ArgumentNullException.ThrowIfNull(substitutionAgent);
        ArgumentNullException.ThrowIfNull(promotionAgent);
        ArgumentNullException.ThrowIfNull(escalationAgent);
        ArgumentNullException.ThrowIfNull(customerMessagingAgent);

#pragma warning disable MAAIW001 // AgentWorkflowBuilder.CreateHandoffBuilderWith is experimental
        Workflow workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(orderIntakeAgent)
            .WithName(WorkflowName)
            .WithDescription("Processes orders through fulfillment and specialist resolution before notifying the customer")
            .WithHandoffs(orderIntakeAgent, [fulfillmentDecisionAgent, customerMessagingAgent])
            .WithHandoffs(
                fulfillmentDecisionAgent,
                [customerMessagingAgent, substitutionAgent, promotionAgent, escalationAgent])
            .WithHandoffs(
                substitutionAgent,
                [customerMessagingAgent, promotionAgent, escalationAgent])
            .WithHandoffs(
                promotionAgent,
                [customerMessagingAgent, escalationAgent])
            .WithHandoff(escalationAgent, customerMessagingAgent)
            .WithAutonomousMode(
                turnLimit: AutonomousTurnLimit,
                continuationPrompt: "Continue processing the order and hand off to the appropriate next agent.")
            .WithTerminationCondition(IsCustomerMessageComplete)
            .WithOutputFrom(customerMessagingAgent)
            .Build();
#pragma warning restore MAAIW001

        return workflow;
    }

    internal static bool IsCustomerMessageComplete(IEnumerable<ChatMessage> conversation) =>
        conversation.Any(message =>
        {
            if (message.Role != ChatRole.Assistant || string.IsNullOrWhiteSpace(message.Text))
            {
                return false;
            }

            try
            {
                CustomerMessageResult? result =
                    JsonSerializer.Deserialize<CustomerMessageResult>(message.Text, JsonOptions);
                return result is not null
                    && !string.IsNullOrWhiteSpace(result.OrderId)
                    && !string.IsNullOrWhiteSpace(result.Message);
            }
            catch (JsonException)
            {
                return false;
            }
        });
}
