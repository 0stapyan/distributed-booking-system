namespace BookingService.Application.Clients;

public interface IGuestScoreClient
{
    Task<GuestScoreDecision> EvaluateAsync(int guestId, int roomId, decimal amount, CancellationToken cancellationToken);
}

public record GuestScoreDecision(bool Approved, int Score, string Decision, string[] Factors);