using DurableAgent.Functions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Extensions
{
    /// <summary>
    /// Extension methods for registering AI agents and durable workflows with the Functions application.
    /// </summary>
    public static class AgentExtensions
    {
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

            Workflow orderProcessingWorkflow = new WorkflowBuilder(orderIntakeAgent)
                                            .WithName("order-processing-workflow")
                                            .WithDescription("Workflow to process customer orders")
                                            .AddEdge(orderIntakeAgent, fulfillmentDecisionAgent)
                                            .AddEdge(fulfillmentDecisionAgent, customerMessagingAgent)
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