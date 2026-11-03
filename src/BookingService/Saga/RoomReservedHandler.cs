using System.Diagnostics;
using System.Text.Json;
using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging;
using Shared.Saga;

namespace BookingService.Saga;

public sealed class RoomReservedHandler : IMessageHandler<RoomReservedEvent>
{
    private readonly BookingServiceDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<RoomReservedHandler> _logger;
    private readonly SagaMetrics _metrics;

    public RoomReservedHandler(BookingServiceDbContext db, IMessageIdAccessor accessor, ILogger<RoomReservedHandler> logger, SagaMetrics metrics)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task HandleAsync(RoomReservedEvent message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate RoomReservedEvent messageId={MessageId} — skipped", messageId);
            return;
        }

        var saga = await _db.BookingSagas.FirstOrDefaultAsync(s => s.CorrelationId == message.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException($"Saga not found for correlationId {message.CorrelationId}");

        if (saga.State != SagaState.Started)
        {
            _logger.LogWarning("[saga] {CorrelationId} unexpected state {State} for room-reserved", message.CorrelationId, saga.State);
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        _metrics.RecordStepDuration((DateTime.UtcNow - saga.UpdatedAt).TotalSeconds, "reserve-room");
        saga.State = SagaState.RoomReserved;
        saga.Amount = message.TotalPrice;
        saga.UpdatedAt = DateTime.UtcNow;
        saga.Version++;

        var booking = await _db.Bookings.FindAsync([message.BookingId], cancellationToken);
        if (booking is not null)
            booking.TotalPrice = message.TotalPrice;

        var command = new ChargePaymentCommand(saga.CorrelationId, message.BookingId, message.TotalPrice);
        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.ChargePaymentKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(command),
            CorrelationId = saga.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(RoomReservedEvent) });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "[saga] {CorrelationId} optimistic lock conflict on room-reserved — will retry", message.CorrelationId);
            throw;
        }

        _logger.LogInformation("[saga] {CorrelationId} room reserved for booking {BookingId}, charging {Amount}", saga.CorrelationId, message.BookingId, message.TotalPrice);
    }
}