using System.Text.Json;
using BookingService.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Cdc;

namespace BookingService.Cdc;

public sealed class GuestReplicaProjector : ICdcProjector
{
    private readonly BookingServiceDbContext _db;
    private readonly ILogger<GuestReplicaProjector> _logger;

    public GuestReplicaProjector(BookingServiceDbContext db, ILogger<GuestReplicaProjector> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProjectAsync(JsonElement row, long lsn, bool deleted, CancellationToken ct)
    {
        var id = CdcMetadata.Int(row, "Id");

        await _db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "GuestReplicas" ("Id", "FirstName", "LastName", "Email", "Phone", "replica_lsn", "is_deleted", "updated_at")
             VALUES (
                 {id},
                 {CdcMetadata.String(row, "FirstName")},
                 {CdcMetadata.String(row, "LastName")},
                 {CdcMetadata.String(row, "Email")},
                 {CdcMetadata.String(row, "Phone")},
                 {lsn},
                 {deleted},
                 NOW()
             )
             ON CONFLICT ("Id") DO UPDATE SET
                 "FirstName"   = EXCLUDED."FirstName",
                 "LastName"    = EXCLUDED."LastName",
                 "Email"       = EXCLUDED."Email",
                 "Phone"       = EXCLUDED."Phone",
                 "replica_lsn" = EXCLUDED."replica_lsn",
                 "is_deleted"  = EXCLUDED."is_deleted",
                 "updated_at"  = NOW()
             WHERE EXCLUDED."replica_lsn" > "GuestReplicas"."replica_lsn"
             """, ct);

        _logger.LogInformation("[cdc] Guests upserted id={Id} lsn={Lsn} deleted={Deleted}", id, lsn, deleted);
    }
}