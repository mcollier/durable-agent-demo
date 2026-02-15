using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DurableAgent.Functions.Tests.TestHelpers;

/// <summary>
/// Minimal fake <see cref="HttpRequestData"/> for unit-testing HTTP-triggered functions.
/// </summary>
public sealed class FakeHttpRequestData : HttpRequestData
{
    private readonly FunctionContext _context;

    public FakeHttpRequestData(
        FunctionContext context,
        string method = "POST",
        string url = "http://localhost/api/feedback",
        Stream? body = null)
        : base(context)
    {
        _context = context;
        Method = method;
        Url = new Uri(url);
        Body = body ?? new MemoryStream();
        Headers = new HttpHeadersCollection();
    }

    public override Stream Body { get; }

    public override HttpHeadersCollection Headers { get; }

    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = Array.Empty<IHttpCookie>();

    public override Uri Url { get; }

    public override IEnumerable<ClaimsIdentity> Identities { get; } = Enumerable.Empty<ClaimsIdentity>();

    public override string Method { get; }

    public override HttpResponseData CreateResponse()
    {
        return new FakeHttpResponseData(_context);
    }
}

/// <summary>
/// Minimal fake <see cref="HttpResponseData"/> returned by <see cref="FakeHttpRequestData.CreateResponse"/>.
/// </summary>
public sealed class FakeHttpResponseData : HttpResponseData
{
    public FakeHttpResponseData(FunctionContext context) : base(context)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
    }

    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers { get; set; }

    public override Stream Body { get; set; }

    public override HttpCookies Cookies { get; } = null!;

    /// <summary>
    /// Reads the response body as a string, resetting the stream position afterward.
    /// </summary>
    public string ReadBodyAsString()
    {
        Body.Position = 0;
        using var reader = new StreamReader(Body, leaveOpen: true);
        var content = reader.ReadToEnd();
        Body.Position = 0;
        return content;
    }
}
