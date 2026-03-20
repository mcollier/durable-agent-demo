using DurableAgent.Functions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Extensions
{
    public static class AgentExtensions
    {
        public static FunctionsApplicationBuilder AddAgents(this FunctionsApplicationBuilder builder)
        {
            CustomerServiceAgentConfig.RegisterAgent(builder);
            EmailAgentConfig.RegisterAgent(builder);
            CustomerMessagingAgentConfig.RegisterAgent(builder);
            FulfillmentDecisionAgentConfig.RegisterAgent(builder);
            OrderIntakeAgentConfig.RegisterAgent(builder);

            return builder;
        }

        public static FunctionsApplicationBuilder AddDurableAgents(this FunctionsApplicationBuilder builder)
        {
            builder.ConfigureDurableAgents(options =>
                options.AddAIAgentFactory(CustomerServiceAgentConfig.AgentName,
                            sp => sp.GetRequiredKeyedService<AIAgent>(CustomerServiceAgentConfig.AgentName))
                       .AddAIAgentFactory(EmailAgentConfig.AgentName,
                            sp => sp.GetRequiredKeyedService<AIAgent>(EmailAgentConfig.AgentName)));

            return builder;
        }
    }
}