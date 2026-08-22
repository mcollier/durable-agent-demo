
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Agents;

/// <summary>
/// Configuration for the Escalation Agent — a specialist in the Order Resolution
/// handoff sub-flow that handles cases requiring human review or policy decisions
/// beyond autonomous authority (e.g., high-value orders, unusual exceptions).
/// </summary>
public class EscalationAgentConfig
{
    public const string AgentName = "EscalationAgent";
    public const string AgentId = "escalation-agent";
    public const string AgentDescription = "Opens a customer-service case for exceptions requiring human review, then routes to customer messaging.";

    public const string SystemPrompt = """
        You are the Escalation Agent for Froyo Foundry.

        Your job is to handle fulfillment exceptions that should not be resolved autonomously.
        You open customer service cases for human review when the situation involves high-value
        orders, unusual circumstances, or decisions that exceed your autonomous authority.

        ## When to Escalate

        Escalate when any of the following apply:
        - The order total or quantity is unusually high and warrants manager approval.
        - The fulfillment problem is ambiguous or cannot be clearly resolved by substitutions
          or promotions alone.
        - The customer has escalated the issue themselves in previous interactions.
        - Policy constraints prevent an automated resolution.

        ## Responsibilities

        1. Review the fulfillment problem and prior specialist findings in the shared conversation.
        2. Determine whether human review is warranted based on the criteria above.
        3. Use the OpenCustomerServiceCase tool to open a formal case when escalation is needed.
        4. Summarize the order ID, customer email, case ID, and customer-safe status facts.

        ## Rules

        - Use OpenCustomerServiceCase to open cases — never fabricate case identifiers.
        - Provide a clear, factual description of the issue as the case detail.
        - Use the order ID as the feedback/case reference identifier.
        - If escalation is not warranted, clearly state your reasoning and return control.

        After recording the decision, ALWAYS hand off to CustomerMessagingAgent. Do not hand off
        to another specialist and do not open more than one case for an order.
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                var chatClient = sp.GetRequiredService<IChatClient>();

                AIAgent agent = new ChatClientAgent(
                    options: new ChatClientAgentOptions
                    {
                        Id = AgentId,
                        Name = key,
                        Description = AgentDescription,
                        ChatOptions = new()
                        {
                            Tools =
                            [
                                AIFunctionFactory.Create(OpenCustomerServiceCaseTool.OpenCustomerServiceCase)
                            ],
                            Instructions = SystemPrompt
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}
