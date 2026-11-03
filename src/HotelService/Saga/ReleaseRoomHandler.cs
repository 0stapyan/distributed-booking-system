using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging;
using Shared.Saga;

namespace HotelService.Saga;

public sealed class ReleaseRoomHandler : IMessageHandler<ReleaseRoomCommand>
{
    private readonly BookingDbContext _db;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<ReleaseRoomHandler> _logger;

    public ReleaseRoomHandler(BookingDbContext db, IMessageIdAccessor accessor, ILogger<ReleaseRoomHandler> logger)
    {
        _db = db;
        _accessor = accessor;
        _logger = logger;
    }

    public async Task HandleAsync(ReleaseRoomCommand message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate ReleaseRoomCommand messageId={MessageId} — skipped", messageId);
            return;
        }

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == message.RoomId, cancellationToken);
        if (room is null)
        {
            _logger.LogWarning("[saga] {CorrelationId} release-room: room {RoomId} not found", message.CorrelationId, message.RoomId);
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        room.IsAvailable = true;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.RoomReleasedKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(new RoomReleasedEvent(message.CorrelationId, message.BookingId, message.RoomId)),
            CorrelationId = message.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(ReleaseRoomCommand) });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("[saga] {CorrelationId} released room {RoomId} for booking {BookingId} — room is available again",
            message.CorrelationId, message.RoomId, message.BookingId);
    }
}