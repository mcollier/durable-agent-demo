using DurableAgent.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Activities;

/// <summary>
/// Sends an escalation email to customer service management.
/// </summary>
public static class SendEscalationEmailActivity
{
    [Function(nameof(SendEscalationEmailActivity))]
    public static string Run(
        [ActivityTrigger] SendEscalationEmailInput input,
        FunctionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(input);

        var logger = executionContext.GetLogger(nameof(SendEscalationEmailActivity));

        // throw new Exception("Simulated exception in SendEscalationEmailActivity for testing retry logic.");

        // TODO: Implement actual email sending (e.g., SendGrid, SMTP, Graph API).
        
        logger.LogInformation(
            "Sending escalation email for case {CaseId} (feedback {FeedbackId}): {Details}",
            input.CaseId,
            input.FeedbackId,
            input.Details);

        return $"Email sent for case {input.CaseId}";
    }
}
