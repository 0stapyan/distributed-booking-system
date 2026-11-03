using NotificationService.Observability;
using Shared.Messaging;
using Shared.Saga;

namespace NotificationService.Workers;

public sealed class BookingConfirmedHandler : IMessageHandler<BookingConfirmedEvent>
{
    private readonly ILogger<BookingConfirmedHandler> _logger;
    private readonly NotificationMetrics _metrics;

    public BookingConfirmedHandler(ILogger<BookingConfirmedHandler> logger, NotificationMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(BookingConfirmedEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[notification] Booking {BookingId} confirmed for saga {CorrelationId}, confirmation sent",
            message.BookingId,
            message.CorrelationId);

        _metrics.RecordConsumed("team-06.saga.booking-confirmed", "success");
        return Task.CompletedTask;
    }
}
