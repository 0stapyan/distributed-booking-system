using System.ComponentModel.DataAnnotations;

namespace BookingService.Models;

public enum SagaState { Started, RoomReserved, PaymentCharged, Completed, Compensating, Cancelled }

public class BookingSaga
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public int BookingId { get; set; }
    public SagaState State { get; set; } = SagaState.Started;
    public string? FailureReason { get; set; }
    public int RoomId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Guests { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public int Version { get; set; }
}
