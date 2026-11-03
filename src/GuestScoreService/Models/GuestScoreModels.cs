namespace GuestScoreService.Models;

public record GuestScoreRequest(int GuestId, int RoomId, decimal Amount);

public record GuestScoreResult(
    bool Approved,
    int Score,
    string Decision,
    string[] Factors,
    GuestFactor Guest,
    PaymentFactor Payment);

public record GuestFactor(int GuestId, bool Exists, int AccountAgeDays);

public record PaymentFactor(int TotalPayments, int FailedPayments, int FailureRatePercent);