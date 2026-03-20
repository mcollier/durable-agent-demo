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
    private readonly ILogger<InboundOrderTrigger> _logger = A.Fake<ILogger<InboundOrderTrigger>>();
    private readonly AIAgent _orderWorkflow = new FakeOrderWorkflowAgent();
    private readonly EmailClient _emailClient = A.Fake<EmailClient>();
    private readonly IOptions<EmailSettings> _emailSettings = Options.Create(new EmailSettings
    {
        RecipientEmailAddress = "test@example.com",
        SenderEmailAddress = "sender@example.com",
        ServiceEndpoint = "https://email.example.com"
    });

    public InboundOrderTriggerTests()
    {
        var sendResult = EmailModelFactory.EmailSendResult("op-1", EmailSendStatus.Succeeded);
        var fakeOperation = A.Fake<EmailSendOperation>();
        A.CallTo(() => fakeOperation.Value).Returns(sendResult);
        A.CallTo(() => _emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .Returns(fakeOperation);
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
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-1");
        var trigger = new InboundOrderTrigger(_logger, _orderWorkflow, _emailClient, _emailSettings);

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
        // "null" deserializes to null for a reference type — triggers the LogWarning path
        var body = BinaryData.FromString("null");
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-null");
        var trigger = new InboundOrderTrigger(_logger, _orderWorkflow, _emailClient, _emailSettings);

        // Should not throw — trigger handles null body gracefully
        await trigger.RunAsync(message, CancellationToken.None);

        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenMessageIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new InboundOrderTrigger(_logger, _orderWorkflow, _emailClient, _emailSettings);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }

    private sealed class FakeOrderWorkflowAgent : AIAgent
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
            return Task.FromResult(new AgentResponse(new List<ChatMessage>()));
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
