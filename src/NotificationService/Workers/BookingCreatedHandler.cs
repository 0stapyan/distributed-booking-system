using NotificationService.Observability;
using Shared.Messaging;

namespace NotificationService.Workers;

public sealed class BookingCreatedHandler : IMessageHandler<BookingCreatedMessage>
{
    private readonly ILogger<BookingCreatedHandler> _logger;
    private readonly NotificationMetrics _metrics;

    public BookingCreatedHandler(ILogger<BookingCreatedHandler> logger, NotificationMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(BookingCreatedMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[notification] Booking #{BookingId} confirmed — room {RoomId}, {Nights} night(s), total {Total:C}",
            message.BookingId,
            message.RoomId,
            (message.CheckOutDate - message.CheckInDate).Days,
            message.TotalPrice);

        _metrics.RecordConsumed("team-06.bookings.booking-created", "success");
        return Task.CompletedTask;
    }
}