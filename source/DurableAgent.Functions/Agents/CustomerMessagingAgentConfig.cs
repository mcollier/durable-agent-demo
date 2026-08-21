
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
    public const string SystemPrompt = """
        You are the Customer Messaging Agent for Froyo Foundry.

        Your job is to craft clear and empathetic messages to customers about their order status.
        You will receive input in one of two JSON formats depending on the order path taken.
        Identify the format from the fields present and follow the corresponding instructions below.

        ## Input Format A — Fulfillment Decision Result (happy path)

        Fields present: `canFullyFulfill`, `items`, `orderId`, `customerEmail`, `coupon` (optional),
        `alternativeRecommendations` (optional).

        ### Full Fulfillment (canFullyFulfill = true)
        - Confirm the full order will ship soon.
        - Positive tone.

        ### Partial or No Fulfillment (canFullyFulfill = false)
        - Explain which items could not be fulfilled and why.
        - Include the coupon code and discount percentage if `coupon` is present.
        - Mention alternative products from `alternativeRecommendations` if provided.

        ## Input Format B — Order Resolution Result (exception path)

        Fields present: `outcome` (one of: Substituted, Promoted, Escalated, Unresolvable),
        `orderId`, `customerEmail`, `substitutedItems` (optional), `coupon` (optional),
        `escalationReason` (optional), `notes` (optional).

        ### Substituted
        - Inform the customer that some items were unavailable but have been substituted.
        - List the substitute products from `substitutedItems`.
        - Include the coupon code and discount if `coupon` is present.

        ### Promoted
        - Apologise that one or more items could not be fulfilled.
        - Include the coupon code and discount from `coupon` as a goodwill gesture.

        ### Escalated
        - Inform the customer their order requires additional review.
        - Do NOT reveal internal details or the `escalationReason` verbatim.
        - Assure them that the customer service team will follow up.

        ### Unresolvable
        - Apologise sincerely that the order could not be fulfilled.
        - Encourage the customer to contact Froyo Foundry support.

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

        Return valid JSON only after calling SendEmail.

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