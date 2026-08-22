using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class InboundOrderTriggerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<InboundOrderTrigger> _logger = A.Fake<ILogger<InboundOrderTrigger>>();

    private static OrderRequest CreateValidOrder() => new()
    {
        OrderReference = "FRY-20260308-AB12",
        FlavorId = "VNE",
        FirstName = "Jane",
        LastName = "Smith",
        StreetAddress = "123 Main St",
        City = "Springfield",
        State = "IL",
        ZipCode = "62701",
        Email = "jane@example.com",
        PhoneNumber = "555-0199"
    };

    private static (IHttpClientFactory factory, FakeHttpMessageHandler handler) CreateFakeHttpClientFactory(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? responseBody = null)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody ?? string.Empty);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var factory = A.Fake<IHttpClientFactory>();
        A.CallTo(() => factory.CreateClient("self")).Returns(client);
        return (factory, handler);
    }

    [Fact]
    public async Task WhenValidMessage_ThenPostsOrderToWorkflowEndpoint()
    {
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-1");
        var (factory, handler) = CreateFakeHttpClientFactory();
        var trigger = new InboundOrderTrigger(_logger, factory);

        await trigger.RunAsync(message, CancellationToken.None);

        Assert.Single(handler.Requests);
        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.method);
        Assert.Equal("http://localhost/api/workflows/order-processing-workflow/run", request.uri);

        var sent = JsonSerializer.Deserialize<OrderRequest>(request.body, JsonOptions);
        Assert.Equal(order.OrderReference, sent?.OrderReference);
    }

    [Fact]
    public async Task WhenValidMessage_ThenLogsOrderReference()
    {
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-1");
        var (factory, _) = CreateFakeHttpClientFactory();
        var trigger = new InboundOrderTrigger(_logger, factory);

        await trigger.RunAsync(message, CancellationToken.None);

        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Information)
            .MustHaveHappenedOnceOrMore();
    }

    [Fact]
    public async Task WhenNullBody_ThenLogsWarningAndDoesNotCallWorkflow()
    {
        var body = BinaryData.FromString("null");
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-null");
        var (factory, handler) = CreateFakeHttpClientFactory();
        var trigger = new InboundOrderTrigger(_logger, factory);

        await trigger.RunAsync(message, CancellationToken.None);

        Assert.Empty(handler.Requests);
        A.CallTo(_logger)
            .Where(call =>
                call.Method.Name == "Log" &&
                call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenMessageIsNull_ThenThrowsArgumentNullException()
    {
        var (factory, _) = CreateFakeHttpClientFactory();
        var trigger = new InboundOrderTrigger(_logger, factory);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task WhenWorkflowReturnsError_ThenThrowsInvalidOperationException()
    {
        var order = CreateValidOrder();
        var body = BinaryData.FromObjectAsJson(order);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: body, messageId: "msg-order-fail");
        var (factory, _) = CreateFakeHttpClientFactory(HttpStatusCode.InternalServerError);
        var trigger = new InboundOrderTrigger(_logger, factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            trigger.RunAsync(message, CancellationToken.None));
    }

    /// <summary>
    /// Test helper that captures HTTP requests and returns a canned response.
    /// </summary>
    internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public List<(HttpMethod method, string uri, string body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;

            Requests.Add((request.Method, request.RequestUri!.ToString(), requestBody));

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
