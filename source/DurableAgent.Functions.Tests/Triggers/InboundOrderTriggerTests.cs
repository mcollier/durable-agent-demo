using System.Text.Json;
using Azure;
using Azure.Communication.Email;
using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Tests.Triggers;

public class InboundOrderTriggerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<InboundOrderTrigger> _logger = A.Fake<ILogger<InboundOrderTrigger>>();
    private readonly EmailClient _emailClient = A.Fake<EmailClient>();
    private readonly IOptions<EmailSettings> _emailSettings = Options.Create(new EmailSettings
    {
        RecipientEmailAddress = "test@example.com",
        SenderEmailAddress = "sender@example.com",
        ServiceEndpoint = "https://email.example.com"
    });

    private EmailSendOperation SetupEmailClientFake()
    {
        var sendResult = EmailModelFactory.EmailSendResult("op-1", EmailSendStatus.Succeeded);
        var fakeOperation = A.Fake<EmailSendOperation>();
        A.CallTo(() => fakeOperation.Value).Returns(sendResult);
        A.CallTo(() => _emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .Returns(fakeOperation);
        return fakeOperation;
    }

    private static OrderRequest CreateValidOrder() => new()
    {
        OrderReference = "FRY-20260308-AB12",
        FlavorId = "VNE",
        FirstName = "Jane",
        LastName = "Smith",
        StreetAddress = "123 Main St",
        City = "Springfield",
        State = "IL",
        ZipCode = "62701",
        Email = "jane@example.com",
        PhoneNumber = "555-0199"
    };

    [Fact]
    public async Task WhenValidMessage_ThenLogsOrderReferenceAndReturns()
    {
        SetupEmailClientFake();
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-1");
        var trigger = new InboundOrderTrigger(_logger, new FakeOrderWorkflowAgent([]), _emailClient, _emailSettings);

        await trigger.RunAsync(message, CancellationToken.None);

        // Verify LogInformation was called (for "Received order {OrderReference}.")
        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Information)
            .MustHaveHappenedOnceOrMore();
    }

    [Fact]
    public async Task WhenNoCustomerMessageProduced_ThenSkipsEmailAndLogsWarning()
    {
        // FakeOrderWorkflowAgent returns an empty message list, so CustomerMessagingAgent
        // never emits a payload — subject and body remain empty.
        var order = CreateValidOrder();
        var msgBody = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: msgBody, messageId: "msg-order-no-agent");
        var trigger = new InboundOrderTrigger(_logger, _orderWorkflow, _emailClient, _emailSettings);

        await trigger.RunAsync(message, CancellationToken.None);

        // Email must NOT be sent when no customer message was produced.
        A.CallTo(() => _emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        // A warning must be logged to indicate the skip.
        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappenedOnceOrMore();
    }

    [Fact]
    public async Task WhenNullBody_ThenLogsWarningAndReturns()
    {
        SetupEmailClientFake();
        // "null" deserializes to null for a reference type — triggers the LogWarning path
        var body = BinaryData.FromString("null");
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-null");
        var trigger = new InboundOrderTrigger(_logger, new FakeOrderWorkflowAgent([]), _emailClient, _emailSettings);

        // Should not throw — trigger handles null body gracefully
        await trigger.RunAsync(message, CancellationToken.None);

        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenAgentReturnsInvalidJson_ThenLogsWarningAndContinues()
    {
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-bad-json");

        // Use an agent that returns invalid JSON from the CustomerMessagingAgent
        var agentWithBadJson = new FakeOrderWorkflowAgentWithInvalidJson();
        var trigger = new InboundOrderTrigger(_logger, agentWithBadJson, _emailClient, _emailSettings);

        // Should not throw — trigger handles JsonException gracefully
        await trigger.RunAsync(message, CancellationToken.None);

        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning &&
                call.GetArgument<object>(2)!.ToString()!.Contains(order.OrderReference ?? string.Empty) &&
                call.GetArgument<object>(2)!.ToString()!.Contains(message.MessageId ?? string.Empty))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenMessageIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new InboundOrderTrigger(_logger, new FakeOrderWorkflowAgent([]), _emailClient, _emailSettings);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task WhenCustomerMessagingAgentReturnsValidPayload_ThenSendsEmailWithExpectedSubjectAndBody()
    {
        // Arrange
        SetupEmailClientFake();
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-email");

        var customerMessage = new CustomerMessageResult
        {
            OrderId = order.OrderReference,
            Message = "<p>Your order is confirmed!</p>"
        };
        var agentChatMessage = new ChatMessage(ChatRole.Assistant, [new TextContent(JsonSerializer.Serialize(customerMessage, JsonOptions))])
        {
            AuthorName = "CustomerMessagingAgent"
        };
        var workflow = new FakeOrderWorkflowAgent([agentChatMessage]);
        var trigger = new InboundOrderTrigger(_logger, workflow, _emailClient, _emailSettings);

        // Act
        await trigger.RunAsync(message, CancellationToken.None);

        // Assert — EmailClient.SendAsync was called with the correct subject and body
        var sendCall = Fake.GetCalls(_emailClient)
            .Single(c => c.Method.Name == nameof(EmailClient.SendAsync));

        var sentMessage = sendCall.GetArgument<EmailMessage>(1);
        Assert.NotNull(sentMessage);
        Assert.Equal($"Update on your order {order.OrderReference}", sentMessage.Content.Subject);
        Assert.Equal(customerMessage.Message, sentMessage.Content.Html);
    }

    [Fact]
    public async Task WhenWorkflowReturnsNoCustomerMessagingAgentMessage_ThenLogsWarningAndDoesNotSendEmail()
    {
        // Arrange — workflow returns messages from other agents, not CustomerMessagingAgent
        SetupEmailClientFake();
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-no-agent");

        var otherMessage = new ChatMessage(ChatRole.Assistant, "Fulfillment decision: fulfilled.")
        {
            AuthorName = "FulfillmentDecisionAgent"
        };
        var workflow = new FakeOrderWorkflowAgent([otherMessage]);
        var trigger = new InboundOrderTrigger(_logger, workflow, _emailClient, _emailSettings);

        // Act
        await trigger.RunAsync(message, CancellationToken.None);

        // Assert — no email was sent
        A.CallTo(() => _emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        // Assert — a warning was logged
        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenCustomerMessagingAgentReturnsInvalidJson_ThenLogsWarningAndDoesNotSendEmail()
    {
        // Arrange — CustomerMessagingAgent returns content that is not valid CustomerMessageResult JSON
        SetupEmailClientFake();
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-bad-json");

        var agentChatMessage = new ChatMessage(ChatRole.Assistant, [new TextContent("not-valid-json{")])
        {
            AuthorName = "CustomerMessagingAgent"
        };
        var workflow = new FakeOrderWorkflowAgent([agentChatMessage]);
        var trigger = new InboundOrderTrigger(_logger, workflow, _emailClient, _emailSettings);

        // Act — should not throw
        await trigger.RunAsync(message, CancellationToken.None);

        // Assert — no email was sent
        A.CallTo(() => _emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        // Assert — a warning was logged (at least one: deserialize failure and/or no-valid-response guard)
        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappenedOnceOrMore();
    }

    private sealed class FakeOrderWorkflowAgent(IReadOnlyList<ChatMessage> responseMessages) : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(A.Fake<AgentSession>());
        }

        protected override ValueTask<System.Text.Json.JsonElement> SerializeSessionCoreAsync(
            AgentSession? session,
            System.Text.Json.JsonSerializerOptions? serializerOptions,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(default(System.Text.Json.JsonElement));
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            System.Text.Json.JsonElement sessionState,
            System.Text.Json.JsonSerializerOptions? serializerOptions,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(A.Fake<AgentSession>());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentResponse([.. responseMessages]));
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeOrderWorkflowAgentWithInvalidJson : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(A.Fake<AgentSession>());

        protected override ValueTask<System.Text.Json.JsonElement> SerializeSessionCoreAsync(
            AgentSession? session,
            System.Text.Json.JsonSerializerOptions? serializerOptions,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(default(System.Text.Json.JsonElement));

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            System.Text.Json.JsonElement sessionState,
            System.Text.Json.JsonSerializerOptions? serializerOptions,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(A.Fake<AgentSession>());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            CancellationToken cancellationToken)
        {
            var badJsonContent = new TextContent("this is not valid JSON {{{");
            var agentMessage = new ChatMessage(ChatRole.Assistant, [badJsonContent])
            {
                AuthorName = "CustomerMessagingAgent"
            };
            return Task.FromResult(new AgentResponse([agentMessage]));
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
