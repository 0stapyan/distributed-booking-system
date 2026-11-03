using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace GuestScoreService.Tests.Support;

public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();

    public Func<HttpRequestMessage, HttpResponseMessage> this[string name]
    {
        set => _handlers[name] = value;
    }

    public Dictionary<string, int> CallCounts { get; } = new();

    private readonly Dictionary<string, Uri> _realClients = new();

    public void SetRealClient(string name, Uri baseAddress) => _realClients[name] = baseAddress;

    public HttpClient CreateClient(string name)
    {
        if (_realClients.TryGetValue(name, out var baseAddress))
            return new HttpClient { BaseAddress = baseAddress };

        if (!_handlers.TryGetValue(name, out var handler))
            throw new InvalidOperationException($"No stub registered for client '{name}'");

        return new HttpClient(new DelegateHandler(handler, () => Count(name)))
        {
            BaseAddress = new Uri($"http://{name}"),
        };
    }

    private void Count(string name) =>
        CallCounts[name] = CallCounts.TryGetValue(name, out var c) ? c + 1 : 1;

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        Action onCall) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onCall();
            return Task.FromResult(handler(request));
        }
    }
}