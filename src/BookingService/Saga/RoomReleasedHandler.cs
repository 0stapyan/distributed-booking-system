using System.Diagnostics;
using System.Text.Json;
using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Models;
using Shared.Saga;

namespace BookingService.Saga;

public sealed class RoomReleasedHandler : IMessageHandler<RoomReleasedEvent>
{
    private readonly BookingServiceDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<RoomReleasedHandler> _logger;
    private readonly SagaMetrics _metrics;

    public RoomReleasedHandler(BookingServiceDbContext db, IMessageIdAccessor accessor, ILogger<RoomReleasedHandler> logger, SagaMetrics metrics)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task HandleAsync(RoomReleasedEvent message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate RoomReleasedEvent messageId={MessageId} — skipped", messageId);
            return;
        }

        var saga = await _db.BookingSagas.FirstOrDefaultAsync(s => s.CorrelationId == message.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga not found for correlationId {message.CorrelationId}");

        if (saga.State != SagaState.Compensating)
        {
            _logger.LogWarning("[saga] {CorrelationId} unexpected state {State} for room-released", message.CorrelationId, saga.State);
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        saga.State = SagaState.Cancelled;
        saga.UpdatedAt = DateTime.UtcNow;
        saga.Version++;

        var booking = await _db.Bookings.FindAsync([message.BookingId], cancellationToken);
        if (booking is not null)
            booking.Status = BookingStatus.Cancelled;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.BookingCancelledKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(new BookingCancelledEvent(saga.CorrelationId, message.BookingId, saga.FailureReason ?? "Payment failed")),
            CorrelationId = saga.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(RoomReleasedEvent) });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "[saga] {CorrelationId} optimistic lock conflict on room-released - will retry", message.CorrelationId);
            throw;
        }

        _metrics.RecordStepDuration((DateTime.UtcNow - saga.CreatedAt).TotalSeconds, "end-to-end");
        _metrics.RecordCompleted("compensated");
        _metrics.DecrementActive();
        _logger.LogInformation("[saga] {CorrelationId} compensation complete → Cancelled. Booking {BookingId} cancelled.",
            saga.CorrelationId, message.BookingId);
    }
}