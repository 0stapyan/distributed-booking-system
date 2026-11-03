namespace NotificationService.Workers;

public record BookingCreatedMessage(
    int BookingId,
    int RoomId,
    decimal TotalPrice,
    DateTime CheckInDate,
    DateTime CheckOutDate);