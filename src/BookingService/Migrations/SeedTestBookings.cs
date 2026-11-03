using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Migrations
{
    public partial class SeedTestBookings : Migration
    {
        // alice  sub: c580f8fb-beee-4906-887e-190539f6825c
        // bob    sub: 0c2b3449-73ed-44ac-9ea3-e2914579ee6c  (used as "other owner" for 403 demo)
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "GuestReplicas" ("Id", "FirstName", "LastName", "Email", "Phone", "replica_lsn", "is_deleted", "updated_at")
                VALUES
                    (1, 'Test', 'Guest',  'guest@example.com',  '+380001234567', 1, false, NOW()),
                    (2, 'Test', 'Guest2', 'guest2@example.com', '+380001234568', 1, false, NOW())
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "RoomReplicas" ("Id", "HotelId", "RoomNumber", "Type", "PricePerNight", "MaxGuests", "IsAvailable", "replica_lsn", "is_deleted", "updated_at")
                VALUES
                    (8, 1, '601', 2, 6000.00, 4, true, 1, false, NOW()),
                    (9, 2, '201', 1, 2200.00, 2, true, 1, false, NOW())
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "Bookings" ("RoomId", "CheckInDate", "CheckOutDate", "Status", "TotalPrice", "CreatedAt", "OwnerId")
                VALUES
                    (8, '2026-08-01 00:00:00+00', '2026-08-05 00:00:00+00', 0, 24000.00, NOW(), 'c580f8fb-beee-4906-887e-190539f6825c'),
                    (9, '2026-08-10 00:00:00+00', '2026-08-15 00:00:00+00', 0, 11000.00, NOW(), '0c2b3449-73ed-44ac-9ea3-e2914579ee6c');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Bookings"
                WHERE "OwnerId" IN (
                    'c580f8fb-beee-4906-887e-190539f6825c',
                    '0c2b3449-73ed-44ac-9ea3-e2914579ee6c'
                );
                DELETE FROM "RoomReplicas"  WHERE "Id" IN (8, 9);
                DELETE FROM "GuestReplicas" WHERE "Id" IN (1, 2);
                """);
        }
    }
}
