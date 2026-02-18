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
    /// <summary>External event name raised when a human reviewer completes their review.</summary>
    internal const string HumanReviewCompletedEvent = "HumanReviewCompleted";
    
    [Function(nameof(FeedbackOrchestrator))]
    public static async Task<string> RunAsync(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        FeedbackMessage feedbackMessage)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(feedbackMessage);

        var logger = context.CreateReplaySafeLogger(nameof(FeedbackOrchestrator));
        logger.LogInformation("Processing feedback {FeedbackId}", feedbackMessage.FeedbackId);

        // Use CustomerServiceAgent to analyze feedback and determine if follow-up is needed
        DurableAIAgent customerServiceAgent = context.GetAgent("CustomerServiceAgent");
        AgentSession agentSession = await customerServiceAgent.CreateSessionAsync();

        AgentResponse<FeedbackResult> agentResponse = await customerServiceAgent.RunAsync<FeedbackResult>(
            message: $"Analyze this customer feedback and provide a summary and sentiment rating: {feedbackMessage}",
            session: agentSession);

        FeedbackResult feedbackResult = agentResponse.Result;

        // if (context.IsReplaying)
        // {
        //     Console.WriteLine($"***REPLAYING**** Received agent response for feedback {feedbackMessage.FeedbackId} during replay: {feedbackResult}");
        // }

        // Check if the feedback result requires human follow-up
        if (feedbackResult.FollowUp?.RequiresHuman == true)
        {
            logger.LogWarning("Feedback {FeedbackId} requires human review. Escalating.", feedbackMessage.FeedbackId);

            bool humanReviewCompleted = await context.WaitForExternalEvent<bool>(HumanReviewCompletedEvent);

            // TODO: Call activity function to get human review result.
        }

        // Prepare email content based on agent analysis and delegate email composition to EmailAgent
        // couponInfo is only included in the prompt if a coupon was generated, to avoid confusion for the agent.
        string couponInfo = feedbackResult.Coupon is { } coupon
            ? $"""
            - Coupon Code: {coupon.Code}
            - Coupon Discount: {coupon.DiscountPercent}%
            - Coupon Expires: {coupon.ExpiresAt:O}
            """
            : "- Coupon: None";

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
            {couponInfo}
            - Original Customer Comment: {feedbackMessage.Comment}
            """;

        // Delegate email composition to the EmailAgent
        DurableAIAgent emailAgent = context.GetAgent("EmailAgent");
        AgentSession emailSession = await emailAgent.CreateSessionAsync();

        AgentResponse<EmailResult> emailResponse = await emailAgent.RunAsync<EmailResult>(
            message: emailPrompt,
            session: emailSession);

        EmailResult emailResult = emailResponse.Result;

        // Send the agent-composed follow-up email to the customer
        await context.CallActivityAsync<string>(
            nameof(SendCustomerEmailActivity),
            new SendCustomerEmailInput
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

