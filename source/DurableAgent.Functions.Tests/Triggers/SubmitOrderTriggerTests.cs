using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DurableAgent.Functions.Models;
using DurableAgent.Functions.Services;
using DurableAgent.Functions.Tests.TestHelpers;
using DurableAgent.Functions.Triggers;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Triggers;

public class SubmitOrderTriggerTests
{
    private readonly IOrderQueueSender _orderQueueSender = A.Fake<IOrderQueueSender>();
    private readonly ILogger<SubmitOrderTrigger> _logger = A.Fake<ILogger<SubmitOrderTrigger>>();
    private readonly FunctionContext _functionContext = A.Fake<FunctionContext>();

    private FakeHttpRequestData CreateRequest(object? body = null)
    {
        var json = body is not null
            ? JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : string.Empty;

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new FakeHttpRequestData(_functionContext, body: stream);
    }

    private static object CreateValidRequestBody() => new
    {
        flavorId = "flavor-001",
        firstName = "Jane",
        lastName = "Smith",
        streetAddress = "123 Main St",
        addressLine2 = "Apt 4B",
        city = "Springfield",
        state = "IL",
        zipCode = "62701",
        email = "jane@example.com",
        phoneNumber = "555-0199",
        orderReference = "FRY-20260308-AB12"
    };

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenValidOrderPayload_ThenReturns200()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(CreateValidRequestBody());

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenValidRequest_ThenEnqueuesOrder()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(CreateValidRequestBody());

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        A.CallTo(() => _orderQueueSender.SendAsync(
                A<OrderRequest>.That.Matches(o =>
                    o.OrderReference == "FRY-20260308-AB12" &&
                    o.FlavorId == "flavor-001" &&
                    o.FirstName == "Jane"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenOptionalFieldsOmitted_ThenReturns200()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        // addressLine2, email, phoneNumber are optional — omitting them still passes validation
        var request = CreateRequest(new
        {
            orderReference = "FRY-20260308-AB12",
            flavorId = "flavor-001",
            firstName = "Jane",
            lastName = "Smith",
            streetAddress = "123 Main St",
            city = "Springfield",
            state = "IL",
            zipCode = "62701"
        });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Validation failures → 400 ────────────────────────────────────────────

    [Fact]
    public async Task WhenAllFieldsNull_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        // "{}" deserializes to an OrderRequest with all required properties null → validation fails
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        var request = new FakeHttpRequestData(_functionContext, body: stream);

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("errors", body);
    }

    [Fact]
    public async Task WhenOrderReferenceOnlyProvided_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        // Only orderReference — FlavorId, name, and address fields are all missing
        var request = CreateRequest(new { orderReference = "FRY-20260308-AB12" });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("errors", body);
    }

    [Fact]
    public async Task WhenOrderReferenceIsMissing_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(new
        {
            flavorId = "flavor-001",
            firstName = "Jane",
            lastName = "Smith",
            streetAddress = "123 Main St",
            city = "Springfield",
            state = "IL",
            zipCode = "62701"
        });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("errors", body);
    }

    [Fact]
    public async Task WhenFlavorIdIsMissing_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(new
        {
            orderReference = "FRY-20260308-AB12",
            firstName = "Jane",
            lastName = "Smith",
            streetAddress = "123 Main St",
            city = "Springfield",
            state = "IL",
            zipCode = "62701"
        });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("errors", body);
    }

    [Fact]
    public async Task WhenFirstNameIsMissing_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(new
        {
            orderReference = "FRY-20260308-AB12",
            flavorId = "flavor-001",
            lastName = "Smith",
            streetAddress = "123 Main St",
            city = "Springfield",
            state = "IL",
            zipCode = "62701"
        });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("errors", body);
    }

    [Fact]
    public async Task WhenAddressFieldsMissing_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        // Omit streetAddress, city, state, zipCode — all are required
        var request = CreateRequest(new
        {
            orderReference = "FRY-20260308-AB12",
            flavorId = "flavor-001",
            firstName = "Jane",
            lastName = "Smith"
        });

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("errors", body);
    }

    // ── Bad input → 400 ─────────────────────────────────────────────────────

    [Fact]
    public async Task WhenInvalidJson_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-json!!!"));
        var request = new FakeHttpRequestData(_functionContext, body: stream);

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("invalid JSON", body);
    }

    [Fact]
    public async Task WhenNullBodyDeserialized_ThenReturns400()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        // "null" literal deserializes JsonSerializer to null for a reference type
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));
        var request = new FakeHttpRequestData(_functionContext, body: stream);

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("empty or null", body);
    }

    // ── Guard clauses ────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenRequestIsNull_ThenThrowsArgumentNullException()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            trigger.RunAsync(null!, CancellationToken.None));
    }

    // ── Service Bus errors ───────────────────────────────────────────────────

    [Fact]
    public async Task WhenTransientServiceBusError_ThenReturns503()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(CreateValidRequestBody());

        A.CallTo(() => _orderQueueSender.SendAsync(A<OrderRequest>.Ignored, A<CancellationToken>.Ignored))
            .Throws(new ServiceBusException("transient", ServiceBusFailureReason.ServiceBusy));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task WhenNonTransientServiceBusError_ThenReturns500()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(CreateValidRequestBody());

        A.CallTo(() => _orderQueueSender.SendAsync(A<OrderRequest>.Ignored, A<CancellationToken>.Ignored))
            .Throws(new ServiceBusException("not transient", ServiceBusFailureReason.MessageSizeExceeded));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task WhenUnexpectedError_ThenReturns500()
    {
        var trigger = new SubmitOrderTrigger(_logger, _orderQueueSender);
        var request = CreateRequest(CreateValidRequestBody());

        A.CallTo(() => _orderQueueSender.SendAsync(A<OrderRequest>.Ignored, A<CancellationToken>.Ignored))
            .Throws(new InvalidOperationException("boom"));

        var response = await trigger.RunAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
