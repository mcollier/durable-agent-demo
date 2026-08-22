using System.Text.Json;
using System.Runtime.CompilerServices;
using DurableAgent.Functions.Workflows;
using FakeItEasy;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace DurableAgent.Functions.Tests.Extensions;

public class OrderWorkflowFactoryTests
{
    [Fact]
    public void WhenWorkflowIsBuilt_ThenItContainsOnlyFourOrderAgents()
    {
        AgentSet agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.CustomerMessaging);

        Assert.Equal(OrderWorkflowFactory.WorkflowName, workflow.Name);
        Assert.Contains("OrderIntakeAgent_order_intake_agent", workflow.ReflectExecutors().Keys);
        Assert.Contains(
            "FulfillmentDecisionAgent_fulfillment_decision_agent",
            workflow.ReflectExecutors().Keys);
        Assert.Contains("SubstitutionAgent_substitution_agent", workflow.ReflectExecutors().Keys);
        Assert.Contains(
            "CustomerMessagingAgent_customer_messaging_agent",
            workflow.ReflectExecutors().Keys);
        Assert.Equal(
            [
                "CustomerMessageOutputExecutor",
                "CustomerMessagingAgent",
                "ForwardToCustomerMessaging",
                "ForwardToFulfillment",
                "ForwardToSubstitution",
                "FulfillmentDecisionAgent",
                "OrderIntakeAgent",
                "OrderWorkflowStartExecutor",
                "SubstitutionAgent"
            ],
            workflow.ReflectExecutors().Keys
                .Select(GetAgentName)
                .Order()
                .ToArray());
    }

    [Fact]
    public void WhenWorkflowIsBuilt_ThenInputAdapterStartsTheWorkflow()
    {
        AgentSet agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.CustomerMessaging);

        Assert.Equal("OrderWorkflowStartExecutor", workflow.StartExecutorId);
        Assert.DoesNotContain("HandoffStart", workflow.ReflectExecutors().Keys);
        Assert.DoesNotContain("HandoffEnd", workflow.ReflectExecutors().Keys);
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
            firstAgents.CustomerMessaging);
        Workflow second = OrderWorkflowFactory.Create(
            secondAgents.OrderIntake,
            secondAgents.FulfillmentDecision,
            secondAgents.Substitution,
            secondAgents.CustomerMessaging);

        Assert.Equal(
            first.ReflectExecutors().Keys.Select(GetAgentName).Order().ToArray(),
            second.ReflectExecutors().Keys.Select(GetAgentName).Order().ToArray());
    }

    [Theory]
    [MemberData(nameof(ValidIntakeMessages))]
    public void WhenIntakeResultIsValid_ThenValidPredicateReturnsTrue(object message)
    {
        Assert.True(OrderWorkflowFactory.IsValidOrder(message));
    }

    public static TheoryData<object> ValidIntakeMessages => new()
    {
        """{"isValid":true,"order":null,"errorMessage":null}""",
        new ChatMessage(ChatRole.Assistant, """{"isValid":true,"order":null,"errorMessage":null}"""),
        new AgentResponse(new ChatMessage(
            ChatRole.Assistant,
            """{"isValid":true,"order":null,"errorMessage":null}""")),
        new List<ChatMessage>
        {
            new(ChatRole.Assistant, """{"isValid":true,"order":null,"errorMessage":null}""")
        }
    };

    [Fact]
    public void WhenIntakeResultIsInvalid_ThenValidPredicateReturnsFalse()
    {
        Assert.False(OrderWorkflowFactory.IsValidOrder(
            """{"isValid":false,"order":null,"errorMessage":"Missing email"}"""));
    }

    [Fact]
    public void WhenFulfillmentCanComplete_ThenFulfillmentPredicateReturnsTrue()
    {
        Assert.True(OrderWorkflowFactory.CanFullyFulfill(
            """{"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":true,"shouldGenerateCoupon":false,"coupon":null,"alternativeRecommendations":[]}"""));
    }

    [Fact]
    public void WhenFulfillmentHasShortfall_ThenFulfillmentPredicateReturnsFalse()
    {
        Assert.False(OrderWorkflowFactory.CanFullyFulfill(
            """{"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":false,"shouldGenerateCoupon":true,"coupon":null,"alternativeRecommendations":[]}"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("""{"unexpected":true}""")]
    public void WhenRoutingOutputIsMalformed_ThenPredicateFailsClearly(string output)
    {
        Assert.Throws<InvalidOperationException>(() => OrderWorkflowFactory.IsValidOrder(output));
        Assert.Throws<InvalidOperationException>(() => OrderWorkflowFactory.CanFullyFulfill(output));
    }

    [Fact]
    public void WhenDurableAgentOutputIsReceived_ThenEnvelopeInputIsExtracted()
    {
        const string DurableInput =
            """["{\"input\":\"{\\\"isValid\\\":true}\",\"state\":{}}"]""";
        JsonElement input = JsonSerializer.Deserialize<JsonElement>(DurableInput);

        string result = AgentTurnMessage.GetText(input);

        Assert.Equal("""{"isValid":true}""", result);
    }

    [Fact]
    public async Task WhenOrderIsInvalid_ThenItRoutesDirectlyToCustomerMessaging()
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(invocations, isValid: false, canFullyFulfill: false);
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.CustomerMessaging);

        string response = await ExecuteWorkflowAsync(workflow, "Process invalid order order-1.");

        Assert.Equal(["OrderIntakeAgent", "CustomerMessagingAgent"], invocations);
        Assert.Contains("order-1", response);
    }

    [Fact]
    public async Task WhenOrderIsFulfillable_ThenItSkipsSubstitution()
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(invocations, isValid: true, canFullyFulfill: true);
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.CustomerMessaging);

        await ExecuteWorkflowAsync(workflow, "Process order order-1.");

        Assert.Equal(
            ["OrderIntakeAgent", "FulfillmentDecisionAgent", "CustomerMessagingAgent"],
            invocations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenOrderHasShortfall_ThenSubstitutionAlwaysRunsBeforeCustomerMessaging(
        bool substituteAvailable)
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(
            invocations,
            isValid: true,
            canFullyFulfill: false,
            substituteAvailable);
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.FulfillmentDecision,
            agents.Substitution,
            agents.CustomerMessaging);

        string response = await ExecuteWorkflowAsync(workflow, "Process order order-1.");

        Assert.Equal(
            [
                "OrderIntakeAgent",
                "FulfillmentDecisionAgent",
                "SubstitutionAgent",
                "CustomerMessagingAgent"
            ],
            invocations);
        Assert.Contains("order-1", response);
    }

    private static AgentSet CreateAgents() => new(
        CreateAgent("OrderIntakeAgent"),
        CreateAgent("FulfillmentDecisionAgent"),
        CreateAgent("SubstitutionAgent"),
        CreateAgent("CustomerMessagingAgent"));

    private static AIAgent CreateAgent(string name) =>
        A.Fake<IChatClient>().AsAIAgent(
            new ChatClientAgentOptions
            {
                Id = GetAgentId(name),
                Name = name,
                Description = $"{name} test agent"
            });

    private static async Task<string> ExecuteWorkflowAsync(Workflow workflow, string input)
    {
        string output = string.Empty;
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow,
            input);

        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
        {
            switch (workflowEvent)
            {
                case WorkflowOutputEvent outputEvent:
                    output = outputEvent.Data switch
                    {
                        ChatMessage message => message.Text,
                        AgentResponse response => response.Text,
                        string text => text,
                        _ => outputEvent.Data?.ToString() ?? string.Empty
                    };
                    break;
                case WorkflowErrorEvent workflowError:
                    throw workflowError.Exception
                        ?? new InvalidOperationException("The workflow failed.");
                case ExecutorFailedEvent executorFailure:
                    throw new InvalidOperationException(
                        $"Executor '{executorFailure.ExecutorId}' failed: {executorFailure.Data}");
            }
        }

        return output;
    }

    private static AgentSet CreateScriptedAgents(
        List<string> invocations,
        bool isValid,
        bool canFullyFulfill,
        bool substituteAvailable = true) => new(
        CreateScriptedAgent(
            "OrderIntakeAgent",
            invocations,
            isValid
                ? """{"isValid":true,"order":null,"errorMessage":null}"""
                : """{"isValid":false,"order":null,"errorMessage":"Missing email"}"""),
        CreateScriptedAgent(
            "FulfillmentDecisionAgent",
            invocations,
            $$"""{"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":{{canFullyFulfill.ToString().ToLowerInvariant()}},"shouldGenerateCoupon":{{(!canFullyFulfill).ToString().ToLowerInvariant()}},"coupon":null,"alternativeRecommendations":[]}"""),
        CreateScriptedAgent(
            "SubstitutionAgent",
            invocations,
            substituteAvailable
                ? """{"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":false,"shouldGenerateCoupon":true,"coupon":{"code":"FRYOCUPON-TEST-25PCT-30D","discountPercent":25},"alternativeRecommendations":[{"sku":"STR-TUB","productName":"Strawberry"}]}"""
                : """{"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":false,"shouldGenerateCoupon":true,"coupon":{"code":"FRYOCUPON-TEST-25PCT-30D","discountPercent":25},"alternativeRecommendations":[]}"""),
        CreateScriptedAgent(
            "CustomerMessagingAgent",
            invocations,
            """{"orderId":"order-1","message":"Your order has been processed."}"""));

    private static AIAgent CreateScriptedAgent(
        string name,
        List<string> invocations,
        string response)
    {
        var client = new ScriptedChatClient(() =>
        {
            invocations.Add(name);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
        });

        return client.AsAIAgent(
            new ChatClientAgentOptions
            {
                Id = GetAgentId(name),
                Name = name,
                Description = $"{name} test agent"
            });
    }

    private sealed record AgentSet(
        AIAgent OrderIntake,
        AIAgent FulfillmentDecision,
        AIAgent Substitution,
        AIAgent CustomerMessaging);

    private static string GetAgentName(string executorId) =>
        executorId.Split('_', 2, StringSplitOptions.None)[0];

    private static string GetAgentId(string name) => name switch
    {
        "OrderIntakeAgent" => "order-intake-agent",
        "FulfillmentDecisionAgent" => "fulfillment-decision-agent",
        "SubstitutionAgent" => "substitution-agent",
        "CustomerMessagingAgent" => "customer-messaging-agent",
        _ => throw new InvalidOperationException($"Unknown test agent {name}.")
    };

    private sealed class ScriptedChatClient(Func<ChatResponse> responseFactory) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responseFactory());

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
