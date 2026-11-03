using System.Net.Http.Json;
using GuestScoreService.Models;

namespace GuestScoreService.Services;

public sealed class GuestScoreEvaluator : IGuestScoreEvaluator
{
    private const int MaxScore = 100;
    private const int ApprovalThreshold = 50;

    private const int UnknownGuestPenalty = 50;
    private const int NewGuestPenalty = 25;
    private const int NewGuestMaxAgeDays = 30;

    private const int PaymentFailureRateThresholdPercent = 20;
    private const int PaymentPenaltyDivisor = 2;

    private readonly HttpClient _guestClient;
    private readonly HttpClient _paymentClient;
    private readonly ILogger<GuestScoreEvaluator> _logger;

    public GuestScoreEvaluator(IHttpClientFactory httpClientFactory, ILogger<GuestScoreEvaluator> logger)
    {
        _guestClient = httpClientFactory.CreateClient("guest-service");
        _paymentClient = httpClientFactory.CreateClient("payment-service");
        _logger = logger;
    }

    public async Task<GuestScoreResult> EvaluateAsync(GuestScoreRequest request, CancellationToken cancellationToken)
    {
        var guestTask = GetGuestFactorAsync(request.GuestId, cancellationToken);
        var paymentTask = GetPaymentFactorAsync(cancellationToken);
        await Task.WhenAll(guestTask, paymentTask);
        var guest = guestTask.Result;
        var payment = paymentTask.Result;

        var factors = new List<string>();
        var score = MaxScore;

        if (!guest.Exists)
        {
            score -= UnknownGuestPenalty;
            factors.Add($"Guest {request.GuestId} has no record");
        }
        else if (guest.AccountAgeDays < NewGuestMaxAgeDays)
        {
            score -= NewGuestPenalty;
            factors.Add($"New guest account ({guest.AccountAgeDays} days old)");
        }

        if (payment.FailureRatePercent > PaymentFailureRateThresholdPercent)
        {
            score -= payment.FailureRatePercent / PaymentPenaltyDivisor;
            factors.Add($"Elevated payment failure rate ({payment.FailedPayments}/{payment.TotalPayments})");
        }

        score = Math.Clamp(score, 0, MaxScore);
        if (factors.Count == 0)
            factors.Add("All checks passed");

        var approved = score >= ApprovalThreshold;
        var decision = approved ? "approved" : "prepayment-required";

        return new GuestScoreResult(approved, score, decision, [.. factors], guest, payment);
    }

    private async Task<GuestFactor> GetGuestFactorAsync(int guestId, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _guestClient.GetFromJsonAsync<GuestSummaryDto>(
                $"/api/internal/guests/{guestId}", cancellationToken);

            if (dto is null || !dto.Exists)
                return new GuestFactor(guestId, false, 0);

            var ageDays = (int)(DateTime.UtcNow - dto.CreatedAt).TotalDays;
            return new GuestFactor(guestId, true, Math.Max(0, ageDays));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "guest-service lookup failed for guest {GuestId}", guestId);
            return new GuestFactor(guestId, false, 0);
        }
    }

    private async Task<PaymentFactor> GetPaymentFactorAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _paymentClient.GetFromJsonAsync<PaymentStatsDto>(
                "/api/internal/payments/stats", cancellationToken);

            if (dto is null || dto.Total == 0)
                return new PaymentFactor(0, 0, 0);

            var rate = (int)Math.Round(100.0 * dto.Failed / dto.Total);
            return new PaymentFactor(dto.Total, dto.Failed, rate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "payment-service stats lookup failed");
            return new PaymentFactor(0, 0, 0);
        }
    }

    private record GuestSummaryDto(int GuestId, bool Exists, DateTime CreatedAt);
    private record PaymentStatsDto(int Total, int Charged, int Failed);
}