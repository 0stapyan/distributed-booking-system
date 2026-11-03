using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Migrations
{
    public partial class AddTraceParentToOutbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "TraceParent" text NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "TraceParent";
                """);
        }
    }
}
