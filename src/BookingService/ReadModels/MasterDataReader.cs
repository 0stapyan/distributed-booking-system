using BookingService.Application.ReadModels;
using BookingService.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingService.ReadModels;

public sealed class MasterDataReader : IMasterDataReader
{
    private readonly BookingServiceDbContext _db;
    private readonly ILogger<MasterDataReader> _logger;

    public MasterDataReader(BookingServiceDbContext db, ILogger<MasterDataReader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RoomView?> GetRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        var replica = await _db.RoomReplicas
            .Where(r => r.Id == roomId && !r.IsDeleted)
            .Select(r => new RoomView(r.Id, r.MaxGuests, r.IsAvailable))
            .FirstOrDefaultAsync(cancellationToken);

        if (replica is null)
            _logger.LogWarning("[replica-cold-start] room {RoomId} not present in local replica yet", roomId);

        return replica;
    }

    public async Task<bool> AllGuestsExistAsync(IReadOnlyCollection<int> guestIds, CancellationToken cancellationToken)
    {
        var found = await _db.GuestReplicas
            .Where(g => guestIds.Contains(g.Id) && !g.IsDeleted)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var allExist = guestIds.All(found.Contains);

        if (!allExist)
        {
            var missing = guestIds.Except(found).ToArray();
            _logger.LogWarning("[replica-cold-start] guests {GuestIds} not present in local replica yet",
                string.Join(",", missing));
        }

        return allExist;
    }
}