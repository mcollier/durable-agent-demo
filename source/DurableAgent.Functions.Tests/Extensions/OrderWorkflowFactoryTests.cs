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
    public void WhenWorkflowIsBuilt_ThenItContainsThreeSequentialAgents()
    {
        AgentSet agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.Fulfillment,
            agents.CustomerMessaging);

        Assert.Equal(OrderWorkflowFactory.WorkflowName, workflow.Name);
        Assert.Contains("OrderIntakeAgent_order_intake_agent", workflow.ReflectExecutors().Keys);
        Assert.Contains("FulfillmentAgent_fulfillment_agent", workflow.ReflectExecutors().Keys);
        Assert.Contains(
            "CustomerMessagingAgent_customer_messaging_agent",
            workflow.ReflectExecutors().Keys);
        Assert.Equal(
            [
                "CustomerMessagingAgent",
                "FulfillmentAgent",
                "OrderIntakeAgent"
            ],
            workflow.ReflectExecutors().Keys
                .Select(GetAgentName)
                .Order()
                .ToArray());
    }

    [Fact]
    public void WhenWorkflowIsBuilt_ThenOrderIntakeIsTheStartNode()
    {
        AgentSet agents = CreateAgents();

        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.Fulfillment,
            agents.CustomerMessaging);

        Assert.Equal("OrderIntakeAgent_order_intake_agent", workflow.StartExecutorId);
        Assert.DoesNotContain(
            workflow.ReflectExecutors().Keys,
            id => id.StartsWith("ForwardTo", StringComparison.Ordinal));
        Assert.DoesNotContain("OrderWorkflowStartExecutor", workflow.ReflectExecutors().Keys);
        Assert.DoesNotContain("CustomerMessageOutputExecutor", workflow.ReflectExecutors().Keys);
    }

    [Fact]
    public void WhenWorkflowIsRebuilt_ThenExecutorIdentitiesRemainStable()
    {
        AgentSet firstAgents = CreateAgents();
        AgentSet secondAgents = CreateAgents();

        Workflow first = OrderWorkflowFactory.Create(
            firstAgents.OrderIntake,
            firstAgents.Fulfillment,
            firstAgents.CustomerMessaging);
        Workflow second = OrderWorkflowFactory.Create(
            secondAgents.OrderIntake,
            secondAgents.Fulfillment,
            secondAgents.CustomerMessaging);

        Assert.Equal(
            first.ReflectExecutors().Keys.Order().ToArray(),
            second.ReflectExecutors().Keys.Order().ToArray());
    }

    [Theory]
    [InlineData(OrderScenario.Invalid)]
    [InlineData(OrderScenario.FullyFulfilled)]
    [InlineData(OrderScenario.Substituted)]
    [InlineData(OrderScenario.NoSubstitute)]
    public async Task WhenOrderIsProcessed_ThenAllThreeAgentsRunInSequence(OrderScenario scenario)
    {
        List<string> invocations = [];
        AgentSet agents = CreateScriptedAgents(invocations, scenario);
        Workflow workflow = OrderWorkflowFactory.Create(
            agents.OrderIntake,
            agents.Fulfillment,
            agents.CustomerMessaging);

        string response = await ExecuteWorkflowAsync(workflow, "Process order order-1.");

        Assert.Equal(
            ["OrderIntakeAgent", "FulfillmentAgent", "CustomerMessagingAgent"],
            invocations);
        Assert.Contains("order-1", response);
    }

    private static AgentSet CreateAgents() => new(
        CreateAgent("OrderIntakeAgent"),
        CreateAgent("FulfillmentAgent"),
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
            new ChatMessage(ChatRole.User, input));
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

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
        OrderScenario scenario) => new(
        CreateScriptedAgent(
            "OrderIntakeAgent",
            invocations,
            scenario == OrderScenario.Invalid
                ? """{"isValid":false,"order":null,"errorMessage":"Missing email"}"""
                : """{"isValid":true,"order":{"orderId":"order-1","customerName":{"firstName":"Ada","middleName":null,"lastName":"Lovelace"},"customerEmail":"customer@example.com","shippingAddress":{"streetAddress":"1 Main St","addressLine2":null,"city":"Columbus","state":"OH","zipCode":"43004"},"lineItems":[{"flavorId":"VNE","quantity":1}]},"errorMessage":null}"""),
        CreateScriptedAgent(
            "FulfillmentAgent",
            invocations,
            GetFulfillmentResponse(scenario)),
        CreateScriptedAgent(
            "CustomerMessagingAgent",
            invocations,
            """{"orderId":"order-1","message":"Your order has been processed."}"""));

    private static string GetFulfillmentResponse(OrderScenario scenario) => scenario switch
    {
        OrderScenario.Invalid =>
            """{"isValidOrder":false,"validationError":"Missing email","orderId":"order-1","customerEmail":null,"items":[],"canFullyFulfill":false,"coupon":null,"alternativeRecommendations":[]}""",
        OrderScenario.FullyFulfilled =>
            """{"isValidOrder":true,"validationError":null,"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":true,"coupon":null,"alternativeRecommendations":[]}""",
        OrderScenario.Substituted =>
            """{"isValidOrder":true,"validationError":null,"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":false,"coupon":{"code":"FRYOCUPON-TEST-15PCT-30D","discountPercent":15},"alternativeRecommendations":[{"sku":"STR-TUB","productName":"Strawberry"}]}""",
        OrderScenario.NoSubstitute =>
            """{"isValidOrder":true,"validationError":null,"orderId":"order-1","customerEmail":"customer@example.com","items":[],"canFullyFulfill":false,"coupon":{"code":"FRYOCUPON-TEST-25PCT-30D","discountPercent":25},"alternativeRecommendations":[]}""",
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
    };

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

    public enum OrderScenario
    {
        Invalid,
        FullyFulfilled,
        Substituted,
        NoSubstitute
    }

    private sealed record AgentSet(
        AIAgent OrderIntake,
        AIAgent Fulfillment,
        AIAgent CustomerMessaging);

    private static string GetAgentName(string executorId) =>
        executorId.Split('_', 2, StringSplitOptions.None)[0];

    private static string GetAgentId(string name) => name switch
    {
        "OrderIntakeAgent" => "order-intake-agent",
        "FulfillmentAgent" => "fulfillment-agent",
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
