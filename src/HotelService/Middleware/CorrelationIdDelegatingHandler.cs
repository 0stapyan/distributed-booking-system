namespace HotelService.Middleware;

public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdHeader] as string
                            ?? Guid.NewGuid().ToString();

        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
