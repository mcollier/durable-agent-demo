using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Activities;

/// <summary>
/// Sends a follow-up email to the customer who submitted feedback.
/// </summary>
public static class SendCustomerEmailActivity
{
    [Function(nameof(SendCustomerEmailActivity))]
    public static string Run(
        [ActivityTrigger] SendCustomerEmailInput input,
        FunctionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(input);

        var logger = executionContext.GetLogger(nameof(SendCustomerEmailActivity));

        // TODO: Implement actual email sending (e.g., SendGrid, SMTP, Graph API).

        logger.LogInformation(
            "Sending follow-up email for case {CaseId} (feedback {FeedbackId}) to {RecipientName} <{RecipientEmail}>",
            input.CaseId,
            input.FeedbackId,
            input.RecipientName,
            input.RecipientEmail);

        return $"Email sent to {input.RecipientEmail} for case {input.CaseId}";
    }
}
