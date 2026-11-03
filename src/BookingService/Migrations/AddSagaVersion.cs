using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Migrations
{
    public partial class AddSagaVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "BookingSagas"
                    ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "FailureReason" text NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "BookingSagas"
                    DROP COLUMN IF EXISTS "Version",
                    DROP COLUMN IF EXISTS "FailureReason";
                """);
        }
    }
}