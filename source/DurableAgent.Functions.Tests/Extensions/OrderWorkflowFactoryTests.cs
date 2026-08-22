using FakeItEasy;
using DurableAgent.Functions.Workflows;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurableAgent.Functions.Tests.Extensions;

public class OrderWorkflowFactoryTests
{
    [Fact]
    public void WhenWorkflowIsBuilt_ThenItContainsOnlyTheSixOrderParticipants()
    {
        var agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);

        Assert.Equal(OrderWorkflowFactory.WorkflowName, workflow.Name);
        Assert.Equal(8, workflow.ReflectExecutors().Count);
        Assert.Equal(
            [
                "CustomerMessagingAgent",
                "EscalationAgent",
                "FulfillmentDecisionAgent",
                "OrderIntakeAgent",
                "PromotionAgent",
                "SubstitutionAgent"
            ],
            workflow.ReflectExecutors().Keys
                .Where(id => id is not "HandoffStart" and not "HandoffEnd")
                .Select(GetAgentName)
                .Order()
                .ToArray());
        Assert.DoesNotContain(
            workflow.ReflectExecutors().Keys,
            id => id.Contains("OrderResolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenWorkflowIsBuilt_ThenOrderIntakeIsTheStartingAgent()
    {
        var agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);

        Assert.Equal("HandoffStart", workflow.StartExecutorId);
        Assert.Contains(
            workflow.ReflectEdges()[workflow.StartExecutorId],
            edge => edge.Connection.SinkIds.Any(
                id => id.StartsWith("OrderIntakeAgent", StringComparison.Ordinal)));
    }

    [Fact]
    public void WhenWorkflowIsBuilt_ThenItUsesHandoffInfrastructureRatherThanASubworkflow()
    {
        var agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);

        Assert.Contains("HandoffStart", workflow.ReflectExecutors().Keys);
        Assert.Contains("HandoffEnd", workflow.ReflectExecutors().Keys);
        Assert.DoesNotContain(
            workflow.ReflectExecutors().Keys,
            id => id.Contains("workflow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenWorkflowIsRebuilt_ThenExecutorIdentitiesRemainStable()
    {
        AgentSet firstAgents = CreateAgents();
        AgentSet secondAgents = CreateAgents();

        Workflow first = OrderWorkflowFactory.Create(
            firstAgents.OrderIntake,
            firstAgents.FulfillmentDecision,
            firstAgents.Substitution,
            firstAgents.Promotion,
            firstAgents.Escalation,
            firstAgents.CustomerMessaging);
        Workflow second = OrderWorkflowFactory.Create(
            secondAgents.OrderIntake,
            secondAgents.FulfillmentDecision,
            secondAgents.Substitution,
            secondAgents.Promotion,
            secondAgents.Escalation,
            secondAgents.CustomerMessaging);

        Assert.Equal(
            first.ReflectExecutors().Keys.Order().ToArray(),
            second.ReflectExecutors().Keys.Order().ToArray());
    }

    [Fact]
    public void WhenConversationIsEmpty_ThenWorkflowDoesNotTerminate()
    {
        Assert.False(OrderWorkflowFactory.IsCustomerMessageComplete([]));
    }

    [Theory]
    [InlineData("The specialist included orderId and message in prose.")]
    [InlineData("{ not valid json")]
    [InlineData("""{"orderId":"","message":"Ready"}""")]
    [InlineData("""{"orderId":"order-1","message":""}""")]
    public void WhenMessageIsNotACompleteCustomerResult_ThenWorkflowDoesNotTerminate(string text)
    {
        ChatMessage message = new(ChatRole.Assistant, text);

        Assert.False(OrderWorkflowFactory.IsCustomerMessageComplete([message]));
    }

    [Fact]
    public void WhenCompleteCustomerResultIsPresent_ThenWorkflowTerminates()
    {
        string json = JsonSerializer.Serialize(new
        {
            orderId = "order-1",
            message = "Your order is ready."
        });
        ChatMessage message = new(ChatRole.Assistant, json);

        Assert.True(OrderWorkflowFactory.IsCustomerMessageComplete([message]));
    }

    [Fact]
    public void WhenCompleteResultComesFromUser_ThenWorkflowDoesNotTerminate()
    {
        ChatMessage message = new(
            ChatRole.User,
            """{"orderId":"order-1","message":"Your order is ready."}""");

        Assert.False(OrderWorkflowFactory.IsCustomerMessageComplete([message]));
    }

    [Fact]
    public async Task WhenOrderIsFulfillable_ThenAgentsHandoffDirectlyToCustomerMessaging()
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(
            invocations,
            fulfillmentTarget: "CustomerMessagingAgent",
            substitutionTarget: null,
            promotionTarget: null);
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);
        AIAgent workflowAgent = workflow.AsAIAgent(id: "test-order-workflow", name: "test-order-workflow");

        AgentResponse response = await workflowAgent.RunAsync("Process order order-1.");

        Assert.True(
            invocations.SequenceEqual(
                ["OrderIntakeAgent", "FulfillmentDecisionAgent", "CustomerMessagingAgent"]),
            $"Unexpected invocation sequence: {string.Join(", ", invocations)}. Response: {response.Text}");
        Assert.Contains("order-1", response.Text);
    }

    [Fact]
    public async Task WhenSubstitutionAndPromotionAreNeeded_ThenBothRunBeforeCustomerMessaging()
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(
            invocations,
            fulfillmentTarget: "SubstitutionAgent",
            substitutionTarget: "PromotionAgent",
            promotionTarget: "CustomerMessagingAgent");
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);
        AIAgent workflowAgent = workflow.AsAIAgent(id: "test-order-workflow", name: "test-order-workflow");

        AgentResponse response = await workflowAgent.RunAsync("Process order order-1.");

        Assert.True(
            invocations.SequenceEqual([
                "OrderIntakeAgent",
                "FulfillmentDecisionAgent",
                "SubstitutionAgent",
                "PromotionAgent",
                "CustomerMessagingAgent"
            ]),
            $"Unexpected invocation sequence: {string.Join(", ", invocations)}. Response: {response.Text}");
    }

    [Fact]
    public async Task WhenOrderIsInvalid_ThenIntakeHandsDirectlyToCustomerMessaging()
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(
            invocations,
            fulfillmentTarget: "CustomerMessagingAgent",
            substitutionTarget: null,
            promotionTarget: null,
            intakeTarget: "CustomerMessagingAgent");
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);
        AIAgent workflowAgent = workflow.AsAIAgent(id: "test-order-workflow", name: "test-order-workflow");

        await workflowAgent.RunAsync("Process invalid order order-1.");

        Assert.Equal(["OrderIntakeAgent", "CustomerMessagingAgent"], invocations);
    }

    [Fact]
    public async Task WhenHumanReviewIsRequired_ThenEscalationRunsBeforeCustomerMessaging()
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(
            invocations,
            fulfillmentTarget: "EscalationAgent",
            substitutionTarget: null,
            promotionTarget: null);
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.Promotion,
            agents.Escalation,
            agents.CustomerMessaging);
        AIAgent workflowAgent = workflow.AsAIAgent(id: "test-order-workflow", name: "test-order-workflow");

        await workflowAgent.RunAsync("Process order order-1.");

        Assert.Equal(
            ["OrderIntakeAgent", "FulfillmentDecisionAgent", "EscalationAgent", "CustomerMessagingAgent"],
            invocations);
    }

    private static AgentSet CreateAgents() => new(
        CreateAgent("order-intake-agent", "OrderIntakeAgent"),
        CreateAgent("fulfillment-decision-agent", "FulfillmentDecisionAgent"),
        CreateAgent("substitution-agent", "SubstitutionAgent"),
        CreateAgent("promotion-agent", "PromotionAgent"),
        CreateAgent("escalation-agent", "EscalationAgent"),
        CreateAgent("customer-messaging-agent", "CustomerMessagingAgent"));

    private static AgentSet CreateScriptedAgents(
        List<string> invocations,
        string fulfillmentTarget,
        string? substitutionTarget,
        string? promotionTarget,
        string intakeTarget = "FulfillmentDecisionAgent") => new(
        CreateScriptedAgent("order-intake-agent", "OrderIntakeAgent", intakeTarget, invocations),
        CreateScriptedAgent(
            "fulfillment-decision-agent",
            "FulfillmentDecisionAgent",
            fulfillmentTarget,
            invocations),
        CreateScriptedAgent("substitution-agent", "SubstitutionAgent", substitutionTarget, invocations),
        CreateScriptedAgent("promotion-agent", "PromotionAgent", promotionTarget, invocations),
        CreateScriptedAgent("escalation-agent", "EscalationAgent", "CustomerMessagingAgent", invocations),
        CreateScriptedAgent("customer-messaging-agent", "CustomerMessagingAgent", null, invocations));

    private static AIAgent CreateAgent(string id, string name)
    {
        return A.Fake<IChatClient>().AsAIAgent(
            new ChatClientAgentOptions
            {
                Id = id,
                Name = name,
                Description = $"{name} test agent"
            });
    }

    private static AIAgent CreateScriptedAgent(
        string id,
        string name,
        string? handoffTarget,
        List<string> invocations)
    {
        var client = new ScriptedChatClient((options) =>
        {
            invocations.Add(name);
            if (handoffTarget is null)
            {
                return new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    """{"orderId":"order-1","message":"Your order has been processed."}"""));
            }

            AITool handoffTool = options?.Tools?.ElementAt(GetHandoffIndex(name, handoffTarget))
                ?? throw new InvalidOperationException($"No handoff tool for {handoffTarget} was provided to {name}.");

            return new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent($"{name}-handoff", handoffTool.Name)]));
        });

        return client.AsAIAgent(
            new ChatClientAgentOptions
            {
                Id = id,
                Name = name,
                Description = $"{name} test agent"
            });
    }

    private static string GetAgentName(string executorId) =>
        executorId.Split('_', 2, StringSplitOptions.None)[0];

    private static int GetHandoffIndex(string source, string target) => (source, target) switch
    {
        ("OrderIntakeAgent", "FulfillmentDecisionAgent") => 0,
        ("OrderIntakeAgent", "CustomerMessagingAgent") => 1,
        ("FulfillmentDecisionAgent", "CustomerMessagingAgent") => 0,
        ("FulfillmentDecisionAgent", "SubstitutionAgent") => 1,
        ("FulfillmentDecisionAgent", "PromotionAgent") => 2,
        ("FulfillmentDecisionAgent", "EscalationAgent") => 3,
        ("SubstitutionAgent", "CustomerMessagingAgent") => 0,
        ("SubstitutionAgent", "PromotionAgent") => 1,
        ("SubstitutionAgent", "EscalationAgent") => 2,
        ("PromotionAgent", "CustomerMessagingAgent") => 0,
        ("PromotionAgent", "EscalationAgent") => 1,
        ("EscalationAgent", "CustomerMessagingAgent") => 0,
        _ => throw new InvalidOperationException($"Unsupported handoff from {source} to {target}.")
    };

    private sealed record AgentSet(
        AIAgent OrderIntake,
        AIAgent FulfillmentDecision,
        AIAgent Substitution,
        AIAgent Promotion,
        AIAgent Escalation,
        AIAgent CustomerMessaging);

    private sealed class ScriptedChatClient(Func<ChatOptions?, ChatResponse> responseFactory) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responseFactory(options));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
