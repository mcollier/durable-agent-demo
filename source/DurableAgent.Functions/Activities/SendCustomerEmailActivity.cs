using Azure;
using Azure.Communication.Email;
using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Activities;

/// <summary>
/// Sends a follow-up email to the customer who submitted feedback via Azure Communication Services.
/// </summary>
public static class SendCustomerEmailActivity
{
    [Function(nameof(SendCustomerEmailActivity))]
    public static async Task<string> RunAsync(
        [ActivityTrigger] SendCustomerEmailInput input,
        FunctionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(input);

        var logger = executionContext.GetLogger(nameof(SendCustomerEmailActivity));
        var emailClient = executionContext.InstanceServices.GetRequiredService<EmailClient>();
        var settings = executionContext.InstanceServices.GetRequiredService<IOptions<EmailSettings>>().Value;

        logger.LogInformation(
            "Sending follow-up email for case {CaseId} (feedback {FeedbackId}) to {RecipientName} <{RecipientEmail}> via {SendToAddress}",
            input.CaseId,
            input.FeedbackId,
            input.RecipientName,
            input.RecipientEmail,
            settings.RecipientEmailAddress);

        var emailMessage = new EmailMessage(
            senderAddress: settings.SenderEmailAddress,
            recipientAddress: settings.RecipientEmailAddress,
            content: new EmailContent(input.Subject)
            {
                PlainText = input.Body,
                Html = input.Body
            });

        try
        {
            EmailSendOperation emailSendOperation =
                await emailClient.SendAsync(WaitUntil.Completed, emailMessage, executionContext.CancellationToken);
            logger.LogInformation(
                "Email send completed with status {Status} for case {CaseId}",
                emailSendOperation.Value.Status,
                input.CaseId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email for case {CaseId} to {RecipientEmail}", input.CaseId, input.RecipientEmail);
            throw;
        }

        return $"Email sent to {settings.RecipientEmailAddress} for case {input.CaseId}";
    }
}
