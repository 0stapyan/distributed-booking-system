using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Migrations
{
    public partial class AddSagaState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "BookingSagas" (
                    "CorrelationId" uuid NOT NULL,
                    "BookingId" integer NOT NULL,
                    "State" text NOT NULL,
                    "RoomId" integer NOT NULL,
                    "Amount" numeric(18,2) NOT NULL,
                    "CheckInDate" timestamp with time zone NOT NULL,
                    "CheckOutDate" timestamp with time zone NOT NULL,
                    "Guests" integer NOT NULL,
                    "PaymentId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_BookingSagas" PRIMARY KEY ("CorrelationId")
                );
                ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "Exchange" text NULL;
                ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "CorrelationId" text NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "BookingSagas";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "Exchange";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "CorrelationId";
                """);
        }
    }
}
