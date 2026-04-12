using Azure;
using Azure.Communication.Email;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Tools;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Tests.Tools;

public class SendEmailToolTests
{
    private static readonly EmailSettings DefaultSettings = new()
    {
        SenderEmailAddress = "sender@froyo.com",
        RecipientEmailAddress = "customer@example.com",
        ServiceEndpoint = "https://email.example.com"
    };

    private static EmailSendOperation CreateFakeEmailSendOperation()
    {
        var sendResult = EmailModelFactory.EmailSendResult("op-1", EmailSendStatus.Succeeded);
        var fakeOperation = A.Fake<EmailSendOperation>();
        A.CallTo(() => fakeOperation.Value).Returns(sendResult);
        return fakeOperation;
    }

    private static (SendEmailTool tool, EmailClient emailClient) CreateTool(
        EmailSettings? settings = null,
        EmailSendStatus? sendStatus = null)
    {
        var sendResult = EmailModelFactory.EmailSendResult("op-1", sendStatus ?? EmailSendStatus.Succeeded);
        var fakeOperation = A.Fake<EmailSendOperation>();
        A.CallTo(() => fakeOperation.Value).Returns(sendResult);

        var emailClient = A.Fake<EmailClient>();
        A.CallTo(() => emailClient.SendAsync(A<WaitUntil>._, A<EmailMessage>._, A<CancellationToken>._))
            .Returns(Task.FromResult(fakeOperation));

        var options = Options.Create(settings ?? DefaultSettings);
        var logger = A.Fake<ILogger<SendEmailTool>>();

        return (new SendEmailTool(emailClient, options, logger), emailClient);
    }

    [Fact]
    public async Task WhenValidInput_ThenCallsEmailClientSendAsync()
    {
        var (tool, emailClient) = CreateTool();

        await tool.SendEmail("Test subject", "<p>Hello</p>");

        A.CallTo(() => emailClient.SendAsync(
                WaitUntil.Completed,
                A<EmailMessage>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenValidInput_ThenSendsFromConfiguredSenderAddress()
    {
        var (tool, emailClient) = CreateTool();
        EmailMessage? captured = null;
        A.CallTo(() => emailClient.SendAsync(WaitUntil.Completed, A<EmailMessage>._, A<CancellationToken>._))
            .Invokes((WaitUntil _, EmailMessage msg, CancellationToken _) => captured = msg)
            .Returns(Task.FromResult(CreateFakeEmailSendOperation()));

        await tool.SendEmail("Subject", "<p>Body</p>");

        Assert.Equal(DefaultSettings.SenderEmailAddress, captured?.SenderAddress);
    }

    [Fact]
    public async Task WhenValidInput_ThenSendsToConfiguredRecipientAddress()
    {
        var (tool, emailClient) = CreateTool();
        EmailMessage? captured = null;
        A.CallTo(() => emailClient.SendAsync(WaitUntil.Completed, A<EmailMessage>._, A<CancellationToken>._))
            .Invokes((WaitUntil _, EmailMessage msg, CancellationToken _) => captured = msg)
            .Returns(Task.FromResult(CreateFakeEmailSendOperation()));

        await tool.SendEmail("Subject", "<p>Body</p>");

        Assert.NotNull(captured);
        Assert.Contains(
            DefaultSettings.RecipientEmailAddress,
            captured.Recipients.To.Select(r => r.Address));
    }

    [Fact]
    public async Task WhenValidInput_ThenSetsHtmlBody()
    {
        var (tool, emailClient) = CreateTool();
        EmailMessage? captured = null;
        A.CallTo(() => emailClient.SendAsync(WaitUntil.Completed, A<EmailMessage>._, A<CancellationToken>._))
            .Invokes((WaitUntil _, EmailMessage msg, CancellationToken _) => captured = msg)
            .Returns(Task.FromResult(CreateFakeEmailSendOperation()));

        await tool.SendEmail("Subject", "<p>Hello customer</p>");

        Assert.Equal("<p>Hello customer</p>", captured?.Content.Html);
    }

    [Fact]
    public async Task WhenValidInput_ThenSetsSubject()
    {
        var (tool, emailClient) = CreateTool();
        EmailMessage? captured = null;
        A.CallTo(() => emailClient.SendAsync(WaitUntil.Completed, A<EmailMessage>._, A<CancellationToken>._))
            .Invokes((WaitUntil _, EmailMessage msg, CancellationToken _) => captured = msg)
            .Returns(Task.FromResult(CreateFakeEmailSendOperation()));

        await tool.SendEmail("Order update FRY-001", "<p>Your order shipped.</p>");

        Assert.Equal("Order update FRY-001", captured?.Content.Subject);
    }

    [Fact]
    public async Task WhenSubjectIsNull_ThenThrowsArgumentNullException()
    {
        var (tool, _) = CreateTool();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            tool.SendEmail(null!, "<p>Body</p>"));
    }

    [Fact]
    public async Task WhenBodyIsNull_ThenThrowsArgumentNullException()
    {
        var (tool, _) = CreateTool();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            tool.SendEmail("Subject", null!));
    }
}
