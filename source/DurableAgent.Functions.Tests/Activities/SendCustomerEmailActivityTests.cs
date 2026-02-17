using DurableAgent.Functions.Activities;
using DurableAgent.Functions.Models;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Activities;

public class SendCustomerEmailActivityTests
{
    private static SendCustomerEmailInput CreateTestInput() => new()
    {
        FeedbackId = "fbk-10021",
        CaseId = "CASE-500",
        RecipientName = "Aidan",
        RecipientEmail = "aidan@example.com",
        Body = "Thank you for your feedback!"
    };

    private static FunctionContext CreateFakeFunctionContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var context = A.Fake<FunctionContext>();
        A.CallTo(() => context.InstanceServices).Returns(serviceProvider);

        return context;
    }

    [Fact]
    public void WhenValidInput_ThenReturnsResultContainingEmailAndCaseId()
    {
        var input = CreateTestInput();
        var context = CreateFakeFunctionContext();

        var result = SendCustomerEmailActivity.Run(input, context);

        Assert.Contains("aidan@example.com", result);
        Assert.Contains("CASE-500", result);
        Assert.StartsWith("Email sent to", result);
    }

    [Fact]
    public void WhenInputIsNull_ThenThrowsArgumentNullException()
    {
        var context = CreateFakeFunctionContext();

        Assert.Throws<ArgumentNullException>(() =>
            SendCustomerEmailActivity.Run(null!, context));
    }

    [Fact]
    public void WhenDifferentRecipient_ThenResultReflectsRecipientEmail()
    {
        var input = CreateTestInput() with { RecipientEmail = "jordan@example.com" };
        var context = CreateFakeFunctionContext();

        var result = SendCustomerEmailActivity.Run(input, context);

        Assert.Contains("jordan@example.com", result);
    }

    [Fact]
    public void WhenEmptyCaseId_ThenResultContainsEmptyCaseId()
    {
        var input = CreateTestInput() with { CaseId = string.Empty };
        var context = CreateFakeFunctionContext();

        var result = SendCustomerEmailActivity.Run(input, context);

        Assert.Equal("Email sent to aidan@example.com for case ", result);
    }
}
