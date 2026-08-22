
using Azure.Communication.Email;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Agents;

public class CustomerMessagingAgentConfig
{
    public const string AgentName = "CustomerMessagingAgent";
    public const string AgentId = "customer-messaging-agent";
    public const string AgentDescription = "Creates and sends the final customer-facing order status message.";
    public const string SystemPrompt = """
        You are the Customer Messaging Agent for Froyo Foundry.

        Create the final customer message from the complete shared conversation history. Use the
        latest factual findings from validation, fulfillment, substitution, promotion, and
        escalation agents. Never invent missing details.

        ## Message Rules

        ### Invalid order
        - Explain the validation problem clearly and how the customer can correct it.
        - Do not imply that inventory was checked.

        ### Full fulfillment
        - Confirm the full order will ship soon.
        - Use a positive tone.

        ### Fulfillment exception
        - Explain unavailable items and confirmed substitutions.
        - Include a generated coupon code and discount percentage when present.
        - If no resolution was possible, apologize and explain the available next step.

        ### Escalation
        - Inform the customer their order requires additional review.
        - Do not reveal internal policy reasoning verbatim.
        - Assure them that the customer service team will follow up.

        ## Writing Style

        Messages must be:
        - clear, concise, polite, and customer-friendly
        - HTML formatted and suitable for sending directly to customers via email

        Do not mention internal systems, agents, tools, workflows, or field names.

        ## Sending the Email

        After crafting the message body, you MUST call the SendEmail tool before returning your JSON output.

        Use the following values when calling SendEmail:
        - subject: "Update on your Froyo Foundry order {orderId}" (replace {orderId} with the actual order ID)
        - body: the HTML-formatted message you composed

        ## Output Requirements

        Return valid JSON only after calling SendEmail. This JSON is the final workflow output;
        do not hand off to another agent.

        Structure:

        {
            "orderId": "string",
            "message": "string"
        }
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                // Get the IChatClient from the DI container
                var chatClient = sp.GetRequiredService<IChatClient>();
                var sendEmailTool = new SendEmailTool(
                    sp.GetRequiredService<EmailClient>(),
                    sp.GetRequiredService<IOptions<EmailSettings>>(),
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<SendEmailTool>());

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
                                AIFunctionFactory.Create(sendEmailTool.SendEmail)
                            ],
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(CustomerMessageResult)),
                                schemaName: "CustomerMessageResult",
                                schemaDescription: "A message to a customer regarding their order fulfillment status, containing the order ID and the message body."
                            )
                        }
                    },
                    chatClient: chatClient
                );

                return agent;
            });
    }
}