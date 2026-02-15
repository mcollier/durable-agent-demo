using DurableAgent.Core.Models;
using DurableAgent.Functions.Activities;
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
        FeedbackMessage input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        
        var logger = context.CreateReplaySafeLogger(nameof(FeedbackOrchestrator));
        logger.LogInformation("Processing feedback {FeedbackId}", input.FeedbackId);

        // var result = await context.CallActivityAsync<string>(
        //     nameof(ProcessFeedbackActivity),
        //     input);

        FeedbackMessage feedback = context.GetInput<FeedbackMessage>() 
            ?? throw new InvalidOperationException("Orchestration input is null or invalid.");

        DurableAIAgent customerServiceAgent = context.GetAgent("CustomerServiceAgent");
        AgentSession agentSession = await customerServiceAgent.CreateSessionAsync();

        AgentResponse<FeedbackResult> agentResponse = await customerServiceAgent.RunAsync<FeedbackResult>(
            message: $"Analyze this customer feedback and provide a summary and sentiment rating: {feedback}",
            session: agentSession);

        var result = await context.CallActivityAsync<string>(
            nameof(ProcessFeedbackActivity),
            input);

        logger.LogInformation("Feedback {FeedbackId} processed: {Result}", input.FeedbackId, result);

        return result;
    }
}
