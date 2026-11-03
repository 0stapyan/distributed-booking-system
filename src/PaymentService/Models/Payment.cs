namespace PaymentService.Models;

public enum PaymentStatus { Charged, Failed, Refunded }

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Charged;
    public Guid CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
