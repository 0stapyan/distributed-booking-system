using GuestScoreService.Models;

namespace GuestScoreService.Services;

public interface IGuestScoreEvaluator
{
    Task<GuestScoreResult> EvaluateAsync(GuestScoreRequest request, CancellationToken cancellationToken);
}