
using System.ComponentModel;
using Azure;
using Azure.Communication.Email;
using DurableAgent.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Tools;

public sealed class SendEmailTool(EmailClient emailClient, IOptions<EmailSettings> emailSettings, ILogger<SendEmailTool> logger)
{
    /// <summary>Sends an HTML email to the configured recipient using Azure Communication Services.</summary>
    [Description("Sends an HTML email to the customer with the specified subject and body.")]
    public async Task SendEmail(
        [Description("The subject of the email.")] string subject,
        [Description("The HTML body content of the email.")] string body)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);

        var settings = emailSettings.Value;

        logger.LogInformation(
            "Sending email. From={Sender} To={Recipient} Subject={Subject} Body={Body}",
            settings.SenderEmailAddress,
            settings.RecipientEmailAddress,
            subject,
            body);

        var message = new EmailMessage(
            senderAddress: settings.SenderEmailAddress,
            recipientAddress: settings.RecipientEmailAddress,
            content: new EmailContent(subject)
            {
                Html = body
            });

        EmailSendOperation emailSendOperation = await emailClient.SendAsync(WaitUntil.Completed, message);

        logger.LogInformation(
            "Email send completed. From={Sender} To={Recipient} Subject={Subject} Status={Status}",
            settings.SenderEmailAddress,
            settings.RecipientEmailAddress,
            subject,
            emailSendOperation.Value.Status);
    }
}