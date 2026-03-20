using DurableAgent.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class EmailAgentConfig
{
    public const string AgentName = "EmailAgent";
    public const string SystemPrompt = """
        # Follow-Up Email Agent — Froyo Foundry

        You write follow-up emails to customers who submitted feedback to Froyo Foundry.
        You **MUST** return a JSON object matching the required schema — no free-form text outside the JSON.

        ## Rules
        - `recipientName` = the customer's preferred name from the input.
        - `recipientEmail` = the customer's email address from the input.
        - `body` = the full email body text (plain text, no HTML).

        ## Tone Guidelines
        - **Positive feedback:** Fun, warm, brand-aligned thank-you.
        - **Neutral feedback:** Appreciative, mention the coupon if one was issued.
        - **Negative / Health-related feedback:** Sincere apology, indicate a representative will review and reach out. Never dismiss health claims, never admit fault or legal liability, never speculate about medical causes.
        - Keep messages concise and professional.

        ## Determinism
        - Use only the data provided in the input. Do not invent names, emails, or details.
        - Do not include explanations outside the JSON.
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                // Get the IChatClient from the DI container
                var chatClient = sp.GetRequiredService<IChatClient>();

                AIAgent agent = new ChatClientAgent(
                   options: new ChatClientAgentOptions
                   {
                       Name = key,
                       ChatOptions = new()
                       {
                           Instructions = SystemPrompt,
                           ResponseFormat = ChatResponseFormat.ForJsonSchema(
                               schema: AIJsonUtilities.CreateJsonSchema(typeof(EmailResult)),
                               schemaName: "EmailResult",
                               schemaDescription: "A follow-up email to a customer who submitted feedback, containing recipient name, email address, subject line, and message body."
                           )
                       }
                   },
                   chatClient: chatClient
               );

                return agent;
            }
        );
    }
}