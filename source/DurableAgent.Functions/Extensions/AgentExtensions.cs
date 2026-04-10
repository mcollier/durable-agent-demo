using DurableAgent.Functions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using SequentialWorkflow;

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
            var customerServiceAgent = builder.Services.BuildServiceProvider()
                .GetRequiredKeyedService<AIAgent>(CustomerServiceAgentConfig.AgentName);

            var emailAgent = builder.Services.BuildServiceProvider()
                .GetRequiredKeyedService<AIAgent>(EmailAgentConfig.AgentName);

            var orderIntakeAgent = builder.Services.BuildServiceProvider()
                .GetRequiredKeyedService<AIAgent>(OrderIntakeAgentConfig.AgentName);

            var fulfillmentDecisionAgent = builder.Services.BuildServiceProvider()
                .GetRequiredKeyedService<AIAgent>(FulfillmentDecisionAgentConfig.AgentName);

            var customerMessagingAgent = builder.Services.BuildServiceProvider()
                .GetRequiredKeyedService<AIAgent>(CustomerMessagingAgentConfig.AgentName);

            Workflow orderProcessingWorkflow = new WorkflowBuilder(orderIntakeAgent)
                                            .WithName("order-processing-workflow")
                                            .WithDescription("Workflow to process customer orders")
                                            .AddEdge(orderIntakeAgent, fulfillmentDecisionAgent)
                                            .AddEdge(fulfillmentDecisionAgent, customerMessagingAgent)
                                            .WithOutputFrom(customerMessagingAgent)
                                            .Build();

            
            // builder.ConfigureDurableAgents(options =>
            //     options.AddAIAgent(customerServiceAgent)
            //            .AddAIAgent(emailAgent));

            // Set up the sample executors
            // OrderLookup orderLookup = new();
            // OrderCancel orderCancel = new();
            // SendEmail sendEmail = new();

            // // Build the CancelOrder workflow: OrderLookup -> OrderCancel -> SendEmail
            // Workflow cancelOrder = new WorkflowBuilder(orderLookup)
            //     .WithName("CancelOrder")
            //     .WithDescription("Cancel an order and notify the customer")
            //     .AddEdge(orderLookup, orderCancel)
            //     .AddEdge(orderCancel, sendEmail)
            //     .Build();

            builder.ConfigureDurableOptions(options =>
            {
                // options.Agents.AddAIAgents([customerServiceAgent, emailAgent]);
                options.Agents.AddAIAgent(customerServiceAgent, enableHttpTrigger: true, enableMcpToolTrigger: false);
                options.Agents.AddAIAgent(emailAgent, enableHttpTrigger: true, enableMcpToolTrigger: false);

                // options.Workflows.AddWorkflow(cancelOrder);
                // options.Workflows.AddWorkflows([orderProcessingWorkflow]);
                options.Workflows.AddWorkflow(orderProcessingWorkflow, exposeStatusEndpoint: true, exposeMcpToolTrigger: false);
            });

            return builder;
        }
    }
}

/*
.ConfigureDurableOptions(options =>
    {
        // Register the standalone agent with HTTP and MCP tool triggers
        options.Agents.AddAIAgent(assistant, enableHttpTrigger: true, enableMcpToolTrigger: true);

        // Register the workflow with an HTTP endpoint and MCP tool trigger
        options.Workflows.AddWorkflow(translateWorkflow, exposeStatusEndpoint: false, exposeMcpToolTrigger: true);
    })
*/