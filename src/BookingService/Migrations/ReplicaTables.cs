using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Migrations
{
    public partial class ReplicaTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "RoomReplicas" (
                    "Id" integer NOT NULL,
                    "HotelId" integer NOT NULL,
                    "RoomNumber" text NOT NULL,
                    "Type" integer NOT NULL,
                    "PricePerNight" numeric(18,2) NOT NULL,
                    "MaxGuests" integer NOT NULL,
                    "IsAvailable" boolean NOT NULL,
                    "replica_lsn" bigint NOT NULL,
                    "is_deleted" boolean NOT NULL,
                    "updated_at" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_RoomReplicas" PRIMARY KEY ("Id")
                );
                CREATE TABLE IF NOT EXISTS "GuestReplicas" (
                    "Id" integer NOT NULL,
                    "FirstName" text NOT NULL,
                    "LastName" text NOT NULL,
                    "Email" text NOT NULL,
                    "Phone" text NOT NULL,
                    "replica_lsn" bigint NOT NULL,
                    "is_deleted" boolean NOT NULL,
                    "updated_at" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_GuestReplicas" PRIMARY KEY ("Id")
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "RoomReplicas";
                DROP TABLE IF EXISTS "GuestReplicas";
                """);
        }
    }
}
