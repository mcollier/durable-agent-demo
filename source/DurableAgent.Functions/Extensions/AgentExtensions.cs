using DurableAgent.Core.Models;
using DurableAgent.Functions.Agents;
using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace DurableAgent.Functions.Extensions
{
    /// <summary>
    /// Extension methods for registering AI agents and durable workflows with the Functions application.
    /// </summary>
    public static class AgentExtensions
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Registers all AI agent configurations into the dependency injection container.
        /// </summary>
        /// <param name="builder">The Functions application builder.</param>
        /// <returns>The <paramref name="builder"/> for chaining.</returns>
        public static FunctionsApplicationBuilder AddAgents(this FunctionsApplicationBuilder builder)
        {
            CustomerServiceAgentConfig.RegisterAgent(builder);
            EmailAgentConfig.RegisterAgent(builder);
            CustomerMessagingAgentConfig.RegisterAgent(builder);
            FulfillmentDecisionAgentConfig.RegisterAgent(builder);
            OrderIntakeAgentConfig.RegisterAgent(builder);
            OrderResolutionAgentConfig.RegisterAgent(builder);
            SubstitutionAgentConfig.RegisterAgent(builder);
            PromotionAgentConfig.RegisterAgent(builder);
            EscalationAgentConfig.RegisterAgent(builder);

            return builder;
        }

        /// <summary>
        /// Resolves registered AI agents and configures them as durable agents and workflows
        /// with HTTP triggers and status endpoints.
        /// </summary>
        /// <param name="builder">The Functions application builder (agents must already be registered via <see cref="AddAgents"/>).</param>
        /// <returns>The <paramref name="builder"/> for chaining.</returns>
        public static FunctionsApplicationBuilder AddDurableAgents(this FunctionsApplicationBuilder builder)
        {
            // ConfigureDurableOptions requires live AIAgent instances, not factories, so we must
            // resolve them before builder.Build(). This creates a temporary container snapshot —
            // all agent registrations must already be complete (i.e. AddAgents() called first).
            var sp = builder.Services.BuildServiceProvider();

            var customerServiceAgent = sp.GetRequiredKeyedService<AIAgent>(CustomerServiceAgentConfig.AgentName);
            var emailAgent = sp.GetRequiredKeyedService<AIAgent>(EmailAgentConfig.AgentName);
            var orderIntakeAgent = sp.GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName);
            var fulfillmentDecisionAgent = sp.GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName);
            var customerMessagingAgent = sp.GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName);
            var orderResolutionAgent = sp.GetRequiredKeyedService<AIAgent>(OrderResolutionAgentConfig.AgentName);
            var substitutionAgent = sp.GetRequiredKeyedService<AIAgent>(SubstitutionAgentConfig.AgentName);
            var promotionAgent = sp.GetRequiredKeyedService<AIAgent>(PromotionAgentConfig.AgentName);
            var escalationAgent = sp.GetRequiredKeyedService<AIAgent>(EscalationAgentConfig.AgentName);

            // Build the Order Resolution handoff sub-workflow.
            // This dynamic sub-flow handles fulfillment exceptions: the OrderResolutionAgent
            // coordinates with Substitution, Promotion, and Escalation specialists via handoffs,
            // then returns a final resolution before CustomerMessaging sends the customer message.
#pragma warning disable MAAIW001 // AgentWorkflowBuilder.CreateHandoffBuilderWith is experimental
            Workflow orderResolutionWorkflow = AgentWorkflowBuilder
                .CreateHandoffBuilderWith(orderResolutionAgent)
                .WithName("order-resolution-workflow")
                .WithDescription("Dynamic handoff sub-flow that resolves fulfillment exceptions via Substitution, Promotion, or Escalation specialists")
                .WithHandoffs(orderResolutionAgent, [substitutionAgent, promotionAgent, escalationAgent])
                .WithHandoffs(substitutionAgent, [orderResolutionAgent])
                .WithHandoffs(promotionAgent, [orderResolutionAgent])
                .WithHandoffs(escalationAgent, [orderResolutionAgent])
                // turnLimit=8: coordinator + up to 3 specialists + return handoffs, with headroom.
                // Default would be 50 turns per agent, which is far too permissive for this bounded sub-flow.
                .WithAutonomousMode(turnLimit: 8, continuationPrompt: "Continue resolving the fulfillment exception.")
                // Stop as soon as any assistant message deserializes cleanly as an OrderResolutionResult
                // with both required fields populated. String-matching on "outcome" alone is too weak —
                // specialist agents may mention "outcome" in natural language during intermediate turns.
                .WithTerminationCondition(conversation =>
                    conversation.Any(m =>
                    {
                        if (m.Role != ChatRole.Assistant || string.IsNullOrWhiteSpace(m.Text)) return false;
                        try
                        {
                            var result = JsonSerializer.Deserialize<OrderResolutionResult>(m.Text, JsonOptions);
                            return result is not null
                                && !string.IsNullOrWhiteSpace(result.OrderId)
                                && !string.IsNullOrWhiteSpace(result.CustomerEmail);
                        }
                        catch (JsonException) { return false; }
                    }))
                .Build();
#pragma warning restore MAAIW001

            // Expose the resolution sub-workflow as an agent so it can participate as
            // a node in the outer WorkflowBuilder graph.
            AIAgent orderResolutionWorkflowAgent = orderResolutionWorkflow.AsAIAgent();

            // Condition: the FulfillmentDecisionAgent output is a ChatMessage whose Text is
            // a JSON-serialized FulfillmentDecisionResult. Route to resolution when there is a shortfall.
            static bool RequiresResolution(object? message)
            {
                var text = message switch
                {
                    ChatMessage cm => cm.Text,
                    AgentResponse ar => ar.Text,
                    _ => message?.ToString()
                };

                if (string.IsNullOrWhiteSpace(text)) return false;

                try
                {
                    var result = JsonSerializer.Deserialize<FulfillmentDecisionResult>(text, JsonOptions);
                    return result is not null && !result.CanFullyFulfill;
                }
                catch (JsonException)
                {
                    return false;
                }
            }

            static bool IsFullyFulfilled(object? message) => !RequiresResolution(message);

            // Build the outer order-processing-workflow with conditional branching:
            //   OrderIntake → FulfillmentDecision
            //     ├─(CanFullyFulfill=true)──────────────────────────→ CustomerMessaging
            //     └─(CanFullyFulfill=false)→ OrderResolution (handoff) → CustomerMessaging
            Workflow orderProcessingWorkflow = new WorkflowBuilder(orderIntakeAgent)
                .WithName("order-processing-workflow")
                .WithDescription("Workflow to process customer orders with dynamic resolution for fulfillment exceptions")
                .AddEdge(orderIntakeAgent, fulfillmentDecisionAgent)
                .AddEdge<object>(fulfillmentDecisionAgent, customerMessagingAgent, condition: IsFullyFulfilled)
                .AddEdge<object>(fulfillmentDecisionAgent, orderResolutionWorkflowAgent, condition: RequiresResolution)
                .AddEdge(orderResolutionWorkflowAgent, customerMessagingAgent)
                .WithOutputFrom(customerMessagingAgent)
                .Build();

            builder.ConfigureDurableOptions(options =>
            {
                options.Agents.AddAIAgent(customerServiceAgent, enableHttpTrigger: true, enableMcpToolTrigger: false);
                options.Agents.AddAIAgent(emailAgent, enableHttpTrigger: true, enableMcpToolTrigger: false);

                options.Workflows.AddWorkflow(orderProcessingWorkflow, exposeStatusEndpoint: true, exposeMcpToolTrigger: false);
            });

            return builder;
        }
    }
}