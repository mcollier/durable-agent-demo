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