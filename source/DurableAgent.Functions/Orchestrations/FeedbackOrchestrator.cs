using DurableAgent.Core.Models;
using DurableAgent.Functions.Activities;
using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Orchestrations;

/// <summary>
/// Orchestrates the processing of a single feedback message.
/// Calls <see cref="ProcessFeedbackActivity"/> to handle the work.
/// </summary>
public static class FeedbackOrchestrator
{
    [Function(nameof(FeedbackOrchestrator))]
    public static async Task<string> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        FeedbackMessage feedbackMessage)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(feedbackMessage);
        
        var logger = context.CreateReplaySafeLogger(nameof(FeedbackOrchestrator));
        logger.LogInformation("Processing feedback {FeedbackId}", feedbackMessage.FeedbackId);

        DurableAIAgent customerServiceAgent = context.GetAgent("CustomerServiceAgent");
        AgentSession agentSession = await customerServiceAgent.CreateSessionAsync();

        // Delegate email composition to the EmailAgent for every feedback submission
        DurableAIAgent emailAgent = context.GetAgent("EmailAgent");
        AgentSession emailSession = await emailAgent.CreateSessionAsync();

        if (context.IsReplaying)
        {
            logger.LogInformation("Replaying orchestration for feedback {FeedbackId}", feedbackMessage.FeedbackId);
        }

        AgentResponse<FeedbackResult> agentResponse = await customerServiceAgent.RunAsync<FeedbackResult>(
            message: $"Analyze this customer feedback and provide a summary and sentiment rating: {feedbackMessage}",
            session: agentSession);

        FeedbackResult feedbackResult = agentResponse.Result;

        DateTime sleepTime = context.CurrentUtcDateTime.AddMinutes(2);
        await context.CreateTimer(sleepTime, CancellationToken.None);

        // Check if the feedback result requires human follow-up
        if (feedbackResult.FollowUp?.RequiresHuman == true)
        {
            logger.LogWarning("Feedback {FeedbackId} requires human review. Escalating.", feedbackMessage.FeedbackId);
        }

        // Simulate an exception to test retry logic in the orchestrator
        // throw new Exception("Simulated exception in FeedbackOrchestrator for testing retry logic.");

        string emailPrompt = $"""
            Write a follow-up email to the customer who submitted the following feedback case:
            - Customer Name: {feedbackMessage.Customer.PreferredName}
            - Customer Email: {feedbackMessage.Customer.Email}
            - Feedback ID: {feedbackMessage.FeedbackId}
            - Case ID: {feedbackResult.FollowUp?.CaseId ?? "N/A"}
            - Sentiment: {feedbackResult.Sentiment}
            - Risk: Health/Safety={feedbackResult.Risk.IsHealthOrSafety}, FoodQuality={feedbackResult.Risk.IsFoodQualityIssue}
            - Keywords: {string.Join(", ", feedbackResult.Risk.Keywords)}
            - Action: {feedbackResult.Action}
            - Original Customer Comment: {feedbackMessage.Comment}
            """;

        AgentResponse<EmailResult> emailResponse = await emailAgent.RunAsync<EmailResult>(
            message: emailPrompt,
            session: emailSession);

        EmailResult emailResult = emailResponse.Result;

        // Send the agent-composed follow-up email to the customer
        await context.CallActivityAsync<string>(
            nameof(SendEscalationEmailActivity),
            new SendEscalationEmailInput
            {
                FeedbackId = feedbackMessage.FeedbackId,
                CaseId = feedbackResult.FollowUp?.CaseId ?? string.Empty,
                RecipientName = emailResult.RecipientName,
                RecipientEmail = emailResult.RecipientEmail,
                Body = emailResult.Body
            });

        var result = await context.CallActivityAsync<string>(
            nameof(ProcessFeedbackActivity),
            feedbackMessage);

        logger.LogInformation("Feedback {FeedbackId} processed: {Result}", feedbackMessage.FeedbackId, result);

        return result;
    }
}

