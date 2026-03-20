using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Communication.Email;
using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using Microsoft.Agents.AI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableAgent.Functions.Triggers;

/// <summary>
/// Receives order messages from the inbound-orders Service Bus queue, runs the order-processing
/// AI agent workflow to determine fulfilment, and sends a follow-up email to the customer
/// via Azure Communication Services.
/// </summary>
public sealed class InboundOrderTrigger(ILogger<InboundOrderTrigger> logger,
                                        [FromKeyedServices("order-processing-workflow")] AIAgent orderWorkflow,
                                        EmailClient emailClient,
                                        IOptions<EmailSettings> emailSettings)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Function(nameof(InboundOrderTrigger))]
    public async Task RunAsync(
        [ServiceBusTrigger("%ORDER_QUEUE_NAME%", Connection = "messaging")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var order = message.Body.ToObjectFromJson<OrderRequest>(JsonOptions);

        if (order is null)
        {
            logger.LogWarning("Received null or empty order message. MessageId={MessageId}", message.MessageId);
            return;
        }

        logger.LogInformation("Received order {OrderReference}.", order.OrderReference);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, $"Determine if this order can be fulfilled: {JsonSerializer.Serialize(order, JsonOptions)}")
        };
        string subject = string.Empty;
        string body = string.Empty;

        var result = await orderWorkflow.RunAsync(messages, cancellationToken: cancellationToken);
        foreach (ChatMessage chatMessage in result.Messages)
        {
            logger.LogInformation("Agent response -- {Role}: {Content}", chatMessage.AuthorName, chatMessage.Contents);

            if (chatMessage.AuthorName == "CustomerMessagingAgent")
            {
                logger.LogInformation("Final message for customer -- {Content}", chatMessage.Contents);

                logger.LogInformation("There are {Count} contents in the message.", chatMessage.Contents?.Count ?? 0);

                if (chatMessage.Contents is not null)
                {
                    foreach (AIContent content in chatMessage.Contents)
                    {
                        if (content is TextContent { Text: { Length: > 0 } text})
                        {
                            CustomerMessageResult? customerMessage = null;
                            try
                            {
                                customerMessage = JsonSerializer.Deserialize<CustomerMessageResult>(text, JsonOptions);
                            }
                            catch (JsonException ex)
                            {
                                logger.LogWarning(ex, "Failed to deserialize CustomerMessagingAgent payload for order {OrderReference}.", order.OrderReference);
                            }
                            if (customerMessage?.Message is not null)
                            {
                                subject = $"Update on your order {customerMessage.OrderId}";
                                body = customerMessage.Message;
                            }
                        }
                    }
                }
            }
        }

        // Guard: if no CustomerMessagingAgent payload was produced, skip sending a blank email.
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
        {
            logger.LogWarning(
                "No customer message was produced by the CustomerMessagingAgent for order {OrderReference}. Skipping email.",
                order.OrderReference);
            return;
        }

        // Send the email to the customer using Azure Communication Services Email SDK
        logger.LogInformation("Sending email for order {OrderReference}. Subject: {Subject}, Body: {Body}", order.OrderReference, subject, body);
        try
        {
            var settings = emailSettings.Value;
            var emailMessage = new EmailMessage(
                senderAddress: settings.SenderEmailAddress,
                recipientAddress: settings.RecipientEmailAddress,
                content: new EmailContent(subject)
                {
                    Html = body
                }
            );

            EmailSendOperation emailSendOperation =
                await emailClient.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken);
            logger.LogInformation("Email send operation completed with status: {Status}", emailSendOperation.Value.Status);
        }
        catch (Exception ex)
        {
            // Email sending is best-effort; log the failure and continue so the
            // Service Bus message is completed and order processing is not retried.
            logger.LogError(ex, "Failed to send email for order {OrderReference}.", order.OrderReference);
        }
    }

    // [Function(nameof(TestWorkflowAsync))]
    // public async Task<HttpResponseData> TestWorkflowAsync(
    //     [HttpTrigger(AuthorizationLevel.Function, "post", Route = "test-workflow")]
    //     HttpRequestData request,
    //     CancellationToken cancellationToken)
    // {
    //     // THIS IS JUST A TEST - DO NOT COMMIT
    //     var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    //     var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

    //     logger.LogInformation("Starting test workflow execution. Endpoint={Endpoint}, Deployment={Deployment}", endpoint, deployment);

    //     OrderRequest? order = null;
    //     try
    //     {
    //         order = await JsonSerializer.DeserializeAsync<OrderRequest>(request.Body, JsonOptions, cancellationToken);
    //     }
    //     catch (JsonException ex)
    //     {
    //         logger.LogWarning(ex, "Failed to deserialize order request body.");
    //     }

    //     if (order is null)
    //     {
    //         var badRequest = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
    //         await badRequest.WriteStringAsync("Request body must be a valid OrderRequest JSON object.", cancellationToken);
    //         return badRequest;
    //     }

    //     var errors = order.Validate();
    //     if (errors.Count > 0)
    //     {
    //         var badRequest = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
    //         await badRequest.WriteAsJsonAsync(new { errors }, cancellationToken);
    //         return badRequest;
    //     }

    //     logger.LogInformation("Processing order {OrderReference} for workflow.", order.OrderReference);

    //     AgentSession session = await orderWorkflow.CreateSessionAsync(cancellationToken);

    //     var messages = new List<ChatMessage>
    //     {
    //         new(ChatRole.User, $"Determine if this order can be fulfilled: {JsonSerializer.Serialize(order, JsonOptions)}")
    //     };

    //     var result = await orderWorkflow.RunAsync(messages, session, cancellationToken: cancellationToken);
    //     // var result = await InProcessExecution.RunAsync(orderWorkflow, messages, cancellationToken: cancellationToken);

    //     // List<ChatMessage> allMessages = [];
    //     // foreach (WorkflowEvent evt in result.NewEvents)
    //     // {
    //     //     // logger.LogInformation("Outgoing events -- {EventType}: {Data}", evt.GetType().Name, evt.Data);

    //     //     if (evt is WorkflowOutputEvent outputEvent)
    //     //     {
    //     //         // logger.LogInformation("Workflow output -- {Data}", outputEvent.Data);

    //     //         allMessages = (List<ChatMessage>)outputEvent.Data!;
    //     //     }
    //     // }

    //     // logger.LogInformation("Total messages in workflow output: {Count}", allMessages.Count);

    //     // foreach (ChatMessage message in allMessages)
    //     // {
    //     //     // log the full ChatMessage object
    //     //     logger.LogInformation("Full ChatMessage object: {Message}", JsonSerializer.Serialize(message, JsonOptions));
    //     //     logger.LogInformation("Agent response -- {Role}: {Content}", message.Role, message.Contents);
    //     // }

    //     foreach (ChatMessage message in result.Messages)
    //     {
    //         logger.LogInformation("Agent response -- {Role}: {Content}", message.AuthorName, message.Contents);
    //     }

    //     // var chatClient = new AzureOpenAIClient(
    //     //     new Uri(endpoint),
    //     //     new DefaultAzureCredential())
    //     // .GetChatClient(deployment)
    //     // .AsIChatClient();

    //     // // Agents
    //     // ChatClientAgent frenchAgent = GetTranslationAgent(chatClient, "French");
    //     // ChatClientAgent germanAgent = GetTranslationAgent(chatClient, "German");

    //     // // build the workflow by adding the executors and connecting them
    //     // var workflow = new WorkflowBuilder(frenchAgent)
    //     //     .AddEdge(frenchAgent, germanAgent)
    //     //     .Build();

    //     // logger.LogInformation("Workflow built successfully. Starting execution...");

    //     // await using Run run = await InProcessExecution.RunAsync(workflow, new ChatMessage(ChatRole.User, "hello world"));

    //     // RunStatus status = await run.GetStatusAsync();
    //     // logger.LogInformation("Workflow execution completed with status: {Status}", status);

    //     // foreach (WorkflowEvent evt in run.OutgoingEvents)
    //     // {
    //     //     logger.LogInformation("Outgoing events -- {EventType}: {Data}", evt.GetType().Name, evt.Data);
    //     //     // if (evt is ExecutorCompletedEvent executorCompleted)
    //     //     // {
    //     //     //     logger.LogInformation("Outgoing events -- {ExecutorId}: {Data}", executorCompleted.ExecutorId, executorCompleted.Data);
    //     //     // }
    //     // }

    //     // foreach (WorkflowEvent evt in run.NewEvents)
    //     // {
    //     //     logger.LogInformation("New events -- {EventType}: {Data}", evt.GetType().Name, evt.Data);

    //     //     if (evt is ExecutorCompletedEvent executorCompleted)
    //     //     {
    //     //         logger.LogInformation("New events -- {ExecutorId}: {Data}", executorCompleted.ExecutorId, executorCompleted.Data);
    //     //     }
    //     // }

    //     // execute the workflow
    //     // await using StreamingRun run = await InProcessExecution.StreamAsync(
    //     //     workflow,
    //     //     new ChatMessage(ChatRole.User, "hello world"));

    //     // logger.LogInformation("Workflow execution started. Watching for events...");

    //     // await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    //     // await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    //     // {
    //     //     if (evt is AgentResponseUpdateEvent executorComplete)
    //     //     {
    //     //         logger.LogInformation("{ExecutorId}: {Data}", executorComplete.ExecutorId, executorComplete.Data);
    //     //     }
    //     // }

    //     var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
    //     await response.WriteStringAsync("Workflow executed successfully.", cancellationToken);

    //     return response;
    // }

    // private ChatClientAgent GetTranslationAgent(IChatClient chatClient, string targetLanguage)
    // {
    //     return new ChatClientAgent(chatClient, $"You are a translation assistant that translates the provided text to {targetLanguage}.");
    // }
}
