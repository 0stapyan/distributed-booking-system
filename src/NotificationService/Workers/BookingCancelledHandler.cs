using NotificationService.Observability;
using Shared.Messaging;
using Shared.Saga;

namespace NotificationService.Workers;

public sealed class BookingCancelledHandler : IMessageHandler<BookingCancelledEvent>
{
    private readonly ILogger<BookingCancelledHandler> _logger;
    private readonly NotificationMetrics _metrics;

    public BookingCancelledHandler(ILogger<BookingCancelledHandler> logger, NotificationMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task HandleAsync(BookingCancelledEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[notification] Booking #{BookingId} CANCELLED for saga {CorrelationId} — reason: {Reason}",
            message.BookingId, message.CorrelationId, message.Reason);

        _metrics.RecordConsumed(SagaTopology.BookingCancelledQueue, "success");
        return Task.CompletedTask;
    }
}