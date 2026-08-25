using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DurableAgent.Functions.Agents;

public class OrderIntakeAgentConfig
{
    public const string AgentName = "OrderIntakeAgent";
    public const string AgentDescription = "Validates incoming orders and returns a structured intake decision.";
    public const string SystemPrompt = """
        You are the Order Intake Agent for Froyo Foundry.

        Validate the incoming order and return valid JSON matching the required response schema.

        ## Business Rules

        - Maximum quantity per item is 10.
        - Minimum quantity per item is 1.
        - Restricted products include "Rainbow Sherbet" and "Chocolate Chip Cookie Dough".
        - Orders must include customer name, email, shipping address, and at least one line item
          with FlavorId and quantity.

        ## Output

        - For a valid order, set isValid to true, populate the canonical order, and set
          errorMessage to null.
        - For an invalid order, set isValid to false, set order to null, and describe every
          validation failure in errorMessage.
        - Preserve the order ID, customer details, shipping address, flavor IDs, and quantities.
        - Return JSON only. Do not discuss routing, workflows, or other agents.
    """;

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
                var chatClient = sp.GetRequiredService<IChatClient>();

                return new ChatClientAgent(
                    options: new ChatClientAgentOptions
                    {
                        Name = key,
                        Description = AgentDescription,
                        ChatOptions = new()
                        {
                            Instructions = SystemPrompt,
                            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                                schema: AIJsonUtilities.CreateJsonSchema(typeof(OrderIntakeResult)),
                                schemaName: nameof(OrderIntakeResult),
                                schemaDescription: "The structured order validation result.")
                        }
                    },
                    chatClient: chatClient);
            });
    }
}
