using System.Diagnostics;
using System.Text.Json;
using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Saga;

namespace BookingService.Saga;

public sealed class PaymentFailedHandler : IMessageHandler<PaymentFailedEvent>
{
    private readonly BookingServiceDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<PaymentFailedHandler> _logger;
    private readonly SagaMetrics _metrics;

    public PaymentFailedHandler(BookingServiceDbContext db, IMessageIdAccessor accessor, ILogger<PaymentFailedHandler> logger, SagaMetrics metrics)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task HandleAsync(PaymentFailedEvent message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate PaymentFailedEvent messageId={MessageId} — skipped", messageId);
            return;
        }

        var saga = await _db.BookingSagas.FirstOrDefaultAsync(s => s.CorrelationId == message.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga not found for correlationId {message.CorrelationId}");

        if (saga.State != SagaState.RoomReserved)
        {
            _logger.LogWarning("[saga] {CorrelationId} unexpected state {State} for payment-failed", message.CorrelationId, saga.State);
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        saga.State = SagaState.Compensating;
        saga.FailureReason = message.Reason;
        saga.UpdatedAt = DateTime.UtcNow;
        saga.Version++;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.ReleaseRoomKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(new ReleaseRoomCommand(saga.CorrelationId, message.BookingId, saga.RoomId)),
            CorrelationId = saga.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(PaymentFailedEvent) });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "[saga] {CorrelationId} optimistic lock conflict on payment-failed - will retry", message.CorrelationId);
            throw;
        }

        _metrics.RecordCompensation("charge-payment");
        _metrics.RecordCompensation("reserve-room");
        _logger.LogInformation("[saga] {CorrelationId} payment failed -> Compensating (releasing room {RoomId}). Reason: {Reason}",
            saga.CorrelationId, saga.RoomId, message.Reason);
    }
}