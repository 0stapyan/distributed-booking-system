namespace Shared.Saga;

public record ReserveRoomCommand(Guid CorrelationId, int BookingId, int RoomId, DateTime CheckIn, DateTime CheckOut, int Guests);
public record ChargePaymentCommand(Guid CorrelationId, int BookingId, decimal Amount);

public record ReleaseRoomCommand(Guid CorrelationId, int BookingId, int RoomId);

public record RoomReservedEvent(Guid CorrelationId, int BookingId, int RoomId, decimal TotalPrice);
public record RoomReservationFailedEvent(Guid CorrelationId, int BookingId, int RoomId, string Reason);
public record PaymentChargedEvent(Guid CorrelationId, int BookingId, Guid PaymentId, decimal Amount);
public record PaymentFailedEvent(Guid CorrelationId, int BookingId, string Reason);

public record RoomReleasedEvent(Guid CorrelationId, int BookingId, int RoomId);

public record BookingConfirmedEvent(Guid CorrelationId, int BookingId);
public record BookingCancelledEvent(Guid CorrelationId, int BookingId, string Reason);