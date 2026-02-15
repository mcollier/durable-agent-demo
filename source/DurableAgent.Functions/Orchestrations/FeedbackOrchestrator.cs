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

        AgentResponse<FeedbackResult> agentResponse = await customerServiceAgent.RunAsync<FeedbackResult>(
            message: $"Analyze this customer feedback and provide a summary and sentiment rating: {feedbackMessage}",
            session: agentSession);

        FeedbackResult feedbackResult = agentResponse.Result;

        // check if the feedback result requires human following
        if (feedbackResult.FollowUp?.RequiresHuman == true)
        {
            logger.LogWarning("Feedback {FeedbackId} requires human review. Escalating.", feedbackMessage.FeedbackId);

            // Send an escalation email to customer service management
            await context.CallActivityAsync<string>(
                nameof(SendEscalationEmailActivity),
                new SendEscalationEmailInput
                {
                    FeedbackId = feedbackMessage.FeedbackId,
                    CaseId = feedbackResult.FollowUp?.CaseId ?? string.Empty,
                    Details = $"Sentiment: {feedbackResult.Sentiment}, Risk: {feedbackResult.Risk}, Action: {feedbackResult.Action}, Message: {feedbackResult.Message.Subject} - {feedbackResult.Message.Body}"
                });
        }

        var result = await context.CallActivityAsync<string>(
            nameof(ProcessFeedbackActivity),
            feedbackMessage);

        logger.LogInformation("Feedback {FeedbackId} processed: {Result}", feedbackMessage.FeedbackId, result);

        return result;
    }
}

