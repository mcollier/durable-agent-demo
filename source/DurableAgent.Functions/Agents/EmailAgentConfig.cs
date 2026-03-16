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
    public const string SystemPrompt = "";

    public static void RegisterAgent(FunctionsApplicationBuilder builder)
    {
        // Get the IChatClient from the DI container
        var chatClient = builder.Services.BuildServiceProvider().GetRequiredService<IChatClient>();

        builder.AddAIAgent(
            name: AgentName,
            (sp, key) =>
            {
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
                                schemaDescription: "A follow-up email to a customer who submitted feedback, containing recipient name, email, and message body."
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