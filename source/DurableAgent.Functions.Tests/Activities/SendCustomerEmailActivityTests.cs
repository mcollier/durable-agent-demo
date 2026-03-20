using Azure;
using Azure.Communication.Email;
using DurableAgent.Functions.Activities;
using DurableAgent.Functions.Models;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Tests.Activities;

public class SendCustomerEmailActivityTests
{
    private static SendCustomerEmailInput CreateTestInput() => new()
    {
        FeedbackId = "fbk-10021",
        CaseId = "CASE-500",
        RecipientName = "Aidan",
        RecipientEmail = "aidan@example.com",
        Subject = "Follow-up on your recent feedback",
        Body = "Thank you for your feedback!"
    };

    private static FunctionContext CreateFakeFunctionContext()
    {
        var sendResult = EmailModelFactory.EmailSendResult("op-1", EmailSendStatus.Succeeded);
        var fakeOperation = A.Fake<EmailSendOperation>();
        A.CallTo(() => fakeOperation.Value).Returns(sendResult);

        var emailClient = A.Fake<EmailClient>();
        A.CallTo(() => emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .Returns(Task.FromResult(fakeOperation));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(emailClient);
        serviceCollection.Configure<EmailSettings>(opts =>
        {
            opts.SenderEmailAddress = "sender@example.com";
            opts.RecipientEmailAddress = "recipient@example.com";
            opts.ServiceEndpoint = "https://example.communication.azure.com";
        });
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var context = A.Fake<FunctionContext>();
        A.CallTo(() => context.InstanceServices).Returns(serviceProvider);

        return context;
    }

    [Fact]
    public async Task WhenValidInput_ThenReturnsResultContainingEmailAndCaseId()
    {
        var input = CreateTestInput();
        var context = CreateFakeFunctionContext();

        var result = await SendCustomerEmailActivity.RunAsync(input, context);

        Assert.Contains("aidan@example.com", result);
        Assert.Contains("CASE-500", result);
        Assert.StartsWith("Email sent to", result);
    }

    [Fact]
    public async Task WhenInputIsNull_ThenThrowsArgumentNullException()
    {
        var context = CreateFakeFunctionContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SendCustomerEmailActivity.RunAsync(null!, context));
    }

    [Fact]
    public async Task WhenDifferentRecipient_ThenResultReflectsRecipientEmail()
    {
        var input = CreateTestInput() with { RecipientEmail = "jordan@example.com" };
        var context = CreateFakeFunctionContext();

        var result = await SendCustomerEmailActivity.RunAsync(input, context);

        Assert.Contains("jordan@example.com", result);
    }

    [Fact]
    public async Task WhenEmptyCaseId_ThenResultContainsEmptyCaseId()
    {
        var input = CreateTestInput() with { CaseId = string.Empty };
        var context = CreateFakeFunctionContext();

        var result = await SendCustomerEmailActivity.RunAsync(input, context);

        Assert.Equal("Email sent to aidan@example.com for case ", result);
    }
}
