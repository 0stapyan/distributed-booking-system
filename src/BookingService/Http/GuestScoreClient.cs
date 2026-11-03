using System.Net.Http.Json;
using BookingService.Application.Clients;
using Polly;

namespace BookingService.Http;

public sealed class GuestScoreClient : IGuestScoreClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ResiliencePipeline<HttpResponseMessage> _resilience;
    private readonly ILogger<GuestScoreClient> _logger;

    public GuestScoreClient(
        HttpClient http,
        IHttpContextAccessor httpContext,
        ResiliencePipeline<HttpResponseMessage> resilience,
        ILogger<GuestScoreClient> logger)
    {
        _http = http;
        _httpContext = httpContext;
        _resilience = resilience;
        _logger = logger;
    }

    public async Task<GuestScoreDecision> EvaluateAsync(int guestId, int roomId, decimal amount, CancellationToken cancellationToken)
    {
        var authorization = _httpContext.HttpContext?.Request.Headers.Authorization.ToString();

        try
        {
            using var response = await _resilience.ExecuteAsync(async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/guest-score")
                {
                    Content = JsonContent.Create(new { guestId, roomId, amount }),
                };
                if (!string.IsNullOrEmpty(authorization))
                    request.Headers.TryAddWithoutValidation("Authorization", authorization);

                return await _http.SendAsync(request, ct);
            }, cancellationToken);

            var decision = await response.Content.ReadFromJsonAsync<GuestScoreDecision>(cancellationToken);
            if (decision is not null)
                return decision;

            _logger.LogWarning("[guest-score] empty response ({Status}) — failing open", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[guest-score] call failed for guest {GuestId} — failing open", guestId);
        }

        return new GuestScoreDecision(Approved: true, Score: 100, Decision: "approved-fail-open", Factors: ["guest-score unavailable"]);
    }
}