using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Models;
using Shared.Saga;

namespace BookingService.Saga;

public sealed class RoomReservationFailedHandler : IMessageHandler<RoomReservationFailedEvent>
{
    private readonly BookingServiceDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<RoomReservationFailedHandler> _logger;
    private readonly SagaMetrics _metrics;

    public RoomReservationFailedHandler(BookingServiceDbContext db, IMessageIdAccessor accessor, ILogger<RoomReservationFailedHandler> logger, SagaMetrics metrics)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task HandleAsync(RoomReservationFailedEvent message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate RoomReservationFailedEvent messageId={MessageId} — skipped", messageId);
            return;
        }

        var saga = await _db.BookingSagas.FirstOrDefaultAsync(s => s.CorrelationId == message.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga not found for correlationId {message.CorrelationId}");

        if (saga.State != SagaState.Started)
        {
            _logger.LogWarning("[saga] {CorrelationId} unexpected state {State} for room-reservation-failed", message.CorrelationId, saga.State);
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        saga.State = SagaState.Cancelled;
        saga.FailureReason = message.Reason;
        saga.UpdatedAt = DateTime.UtcNow;
        saga.Version++;

        var booking = await _db.Bookings.FindAsync([message.BookingId], cancellationToken);
        if (booking is not null)
            booking.Status = BookingStatus.Cancelled;

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(RoomReservationFailedEvent) });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "[saga] {CorrelationId} optimistic lock conflict on room-reservation-failed — will retry", message.CorrelationId);
            throw;
        }

        _metrics.RecordCompleted("failed");
        _metrics.DecrementActive();
        _logger.LogWarning("[saga] {CorrelationId} cancelled — room reservation failed: {Reason}", message.CorrelationId, message.Reason);
    }
}