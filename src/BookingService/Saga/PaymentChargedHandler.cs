using System.Diagnostics;
using System.Text.Json;
using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Models;
using Shared.Saga;

namespace BookingService.Saga;

public sealed class PaymentChargedHandler : IMessageHandler<PaymentChargedEvent>
{
    private readonly BookingServiceDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<PaymentChargedHandler> _logger;
    private readonly SagaMetrics _metrics;

    public PaymentChargedHandler(BookingServiceDbContext db, IMessageIdAccessor accessor, ILogger<PaymentChargedHandler> logger, SagaMetrics metrics)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task HandleAsync(PaymentChargedEvent message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate PaymentChargedEvent messageId={MessageId} — skipped", messageId);
            return;
        }

        var saga = await _db.BookingSagas.FirstOrDefaultAsync(s => s.CorrelationId == message.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga not found for correlationId {message.CorrelationId}");

        if (saga.State != SagaState.RoomReserved)
        {
            _logger.LogWarning("[saga] {CorrelationId} unexpected state {State} for payment-charged", message.CorrelationId, saga.State);
            _metrics.RecordCompleted("failed");
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        _metrics.RecordStepDuration((DateTime.UtcNow - saga.UpdatedAt).TotalSeconds, "charge-payment");
        saga.State = SagaState.Completed;
        saga.PaymentId = message.PaymentId;
        saga.UpdatedAt = DateTime.UtcNow;
        saga.Version++;

        var booking = await _db.Bookings.FindAsync([message.BookingId], cancellationToken);
        if (booking is not null)
            booking.Status = BookingStatus.Confirmed;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.BookingConfirmedKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(new BookingConfirmedEvent(saga.CorrelationId, message.BookingId)),
            CorrelationId = saga.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(PaymentChargedEvent) });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "[saga] {CorrelationId} optimistic lock conflict on payment-charged, will retry", message.CorrelationId);
            throw;
        }

        _metrics.RecordCompleted("completed");
        _metrics.DecrementActive();
        _metrics.RecordStepDuration((DateTime.UtcNow - saga.CreatedAt).TotalSeconds, "end-to-end");
        _logger.LogInformation("[saga] {CorrelationId} PIVOT — completed, booking {BookingId} confirmed", saga.CorrelationId, message.BookingId);
    }
}