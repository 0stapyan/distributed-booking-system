using System.Text.Json;
using BookingService.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Cdc;

namespace BookingService.Cdc;

public sealed class RoomReplicaProjector : ICdcProjector
{
    private readonly BookingServiceDbContext _db;
    private readonly ILogger<RoomReplicaProjector> _logger;

    public RoomReplicaProjector(BookingServiceDbContext db, ILogger<RoomReplicaProjector> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProjectAsync(JsonElement row, long lsn, bool deleted, CancellationToken ct)
    {
        var id = CdcMetadata.Int(row, "Id");

        await _db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "RoomReplicas" ("Id", "HotelId", "RoomNumber", "Type", "PricePerNight", "MaxGuests", "IsAvailable", "replica_lsn", "is_deleted", "updated_at")
             VALUES (
                 {id},
                 {CdcMetadata.Int(row, "HotelId")},
                 {CdcMetadata.String(row, "RoomNumber")},
                 {CdcMetadata.Int(row, "Type")},
                 {CdcMetadata.Decimal(row, "PricePerNight")},
                 {CdcMetadata.Int(row, "MaxGuests")},
                 {CdcMetadata.Bool(row, "IsAvailable")},
                 {lsn},
                 {deleted},
                 NOW()
             )
             ON CONFLICT ("Id") DO UPDATE SET
                 "HotelId"       = EXCLUDED."HotelId",
                 "RoomNumber"    = EXCLUDED."RoomNumber",
                 "Type"          = EXCLUDED."Type",
                 "PricePerNight" = EXCLUDED."PricePerNight",
                 "MaxGuests"     = EXCLUDED."MaxGuests",
                 "IsAvailable"   = EXCLUDED."IsAvailable",
                 "replica_lsn"   = EXCLUDED."replica_lsn",
                 "is_deleted"    = EXCLUDED."is_deleted",
                 "updated_at"    = NOW()
             WHERE EXCLUDED."replica_lsn" > "RoomReplicas"."replica_lsn"
             """, ct);

        _logger.LogInformation("[cdc] Rooms upserted id={Id} lsn={Lsn} deleted={Deleted}", id, lsn, deleted);
    }
}