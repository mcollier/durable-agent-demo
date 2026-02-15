using Azure.Messaging.ServiceBus;
using DurableAgent.Core.Models;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class InboundFeedbackTriggerTests
{
    private readonly ILogger<InboundFeedbackTrigger> _logger = A.Fake<ILogger<InboundFeedbackTrigger>>();
    private readonly DurableTaskClient _durableClient = A.Fake<DurableTaskClient>();

    private static FeedbackMessage CreateTestFeedback() => new()
    {
        FeedbackId = "fbk-10021",
        StoreId = "store-014",
        OrderId = "ord-77812",
        Customer = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        },
        Channel = "kiosk",
        Rating = 5,
        Comment = "Mint Condition is unreal. Best froyo I've had. Staff was super nice!"
    };

    [Fact]
    public async Task WhenValidMessageReceived_ThenSchedulesOrchestration()
    {
        // Arrange
        var feedback = CreateTestFeedback();
        var body = BinaryData.FromObjectAsJson(feedback);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-1");

        A.CallTo(() => _durableClient.ScheduleNewOrchestrationInstanceAsync(
                A<TaskName>.Ignored,
                A<object?>.Ignored,
                A<StartOrchestrationOptions?>.Ignored,
                A<CancellationToken>.Ignored))
            .Returns("instance-001");

        var trigger = new InboundFeedbackTrigger(_logger);

        // Act
        await trigger.RunAsync(message, _durableClient, CancellationToken.None);

        // Assert — capture the call and verify arguments
        var call = Fake.GetCalls(_durableClient)
            .Single(c => c.Method.Name == nameof(DurableTaskClient.ScheduleNewOrchestrationInstanceAsync));

        var actualName = call.GetArgument<TaskName>(0);
        var actualInput = call.GetArgument<object?>(1) as FeedbackMessage;

        Assert.Equal("FeedbackOrchestrator", actualName.Name);
        Assert.NotNull(actualInput);
        Assert.Equal("fbk-10021", actualInput.FeedbackId);
        Assert.Equal("Mint Condition is unreal. Best froyo I've had. Staff was super nice!", actualInput.Comment);
        Assert.Equal("store-014", actualInput.StoreId);
        Assert.Equal(5, actualInput.Rating);
        Assert.NotNull(actualInput.Customer);
        Assert.Equal("Aidan", actualInput.Customer.PreferredName);
    }

    [Fact]
    public async Task WhenMessageIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new InboundFeedbackTrigger(_logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, _durableClient, CancellationToken.None));
    }

    [Fact]
    public async Task WhenDurableClientIsNull_ThenThrowsArgumentNullException()
    {
        var body = BinaryData.FromObjectAsJson(CreateTestFeedback());
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-2");
        var trigger = new InboundFeedbackTrigger(_logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(message, null!, CancellationToken.None));
    }

    [Fact]
    public async Task WhenMessageBodyIsNullFeedback_ThenLogsWarningAndReturns()
    {
        // Arrange — body that deserializes to null
        var body = BinaryData.FromString("null");
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-null");
        var trigger = new InboundFeedbackTrigger(_logger);

        // Act — should not throw, should log warning and return early
        await trigger.RunAsync(message, _durableClient, CancellationToken.None);

        // Assert — no orchestration was scheduled
        var scheduleCalls = Fake.GetCalls(_durableClient)
            .Where(c => c.Method.Name == nameof(DurableTaskClient.ScheduleNewOrchestrationInstanceAsync));
        Assert.Empty(scheduleCalls);
    }
}
