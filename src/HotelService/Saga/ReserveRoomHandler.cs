using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging;
using Shared.Saga;

namespace HotelService.Saga;

public sealed class ReserveRoomHandler : IMessageHandler<ReserveRoomCommand>
{
    private readonly BookingDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<ReserveRoomHandler> _logger;

    public ReserveRoomHandler(BookingDbContext db, IMessageIdAccessor accessor, ILogger<ReserveRoomHandler> logger)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
    }

    public async Task HandleAsync(ReserveRoomCommand message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate ReserveRoomCommand messageId={MessageId} — skipped", messageId);
            return;
        }

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == message.RoomId, cancellationToken);

        if (room is null || !room.IsAvailable || message.Guests > room.MaxGuests)
        {
            var reason = room is null ? "Room not found"
                : !room.IsAvailable ? "Room is not available"
                : "Room capacity exceeded";

            _db.OutboxMessages.Add(new OutboxMessage
            {
                EventType = SagaTopology.RoomReservationFailedKey,
                Exchange = SagaTopology.Exchange,
                Payload = JsonSerializer.Serialize(new RoomReservationFailedEvent(message.CorrelationId, message.BookingId, message.RoomId, reason)),
                CorrelationId = message.CorrelationId.ToString(),
                CreatedAt = DateTime.UtcNow,
                TraceParent = Activity.Current?.Id,
            });

            _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(ReserveRoomCommand) });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            _logger.LogWarning("[saga] {CorrelationId} room {RoomId} reservation failed: {Reason}", message.CorrelationId, message.RoomId, reason);
            return;
        }

        room.IsAvailable = false;

        var nights = (message.CheckOut - message.CheckIn).Days;
        var totalPrice = room.PricePerNight * nights;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.RoomReservedKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(new RoomReservedEvent(message.CorrelationId, message.BookingId, message.RoomId, totalPrice)),
            CorrelationId = message.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(ReserveRoomCommand) });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("[saga] {CorrelationId} reserved room {RoomId} for booking {BookingId}, total {Total}", message.CorrelationId, message.RoomId, message.BookingId, totalPrice);
    }
}