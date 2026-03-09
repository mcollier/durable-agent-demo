using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class InboundOrderTriggerTests
{
    private readonly ILogger<InboundOrderTrigger> _logger = A.Fake<ILogger<InboundOrderTrigger>>();

    private static OrderRequest CreateValidOrder() => new()
    {
        OrderReference = "FRY-20260308-AB12",
        FlavorId = "flavor-001",
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
        var trigger = new InboundOrderTrigger(_logger);

        await trigger.RunAsync(message, CancellationToken.None);

        // Verify LogInformation was called (for "Received order {OrderReference}.")
        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Information)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenNullBody_ThenLogsWarningAndReturns()
    {
        // "null" deserializes to null for a reference type — triggers the LogWarning path
        var body = BinaryData.FromString("null");
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-null");
        var trigger = new InboundOrderTrigger(_logger);

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
        var trigger = new InboundOrderTrigger(_logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }
}
