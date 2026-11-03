using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingService.Migrations
{
    public partial class AddInboxMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "InboxMessages" (
                    "Id" uuid NOT NULL,
                    "MessageType" character varying(256) NOT NULL,
                    "ProcessedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_InboxMessages" PRIMARY KEY ("Id")
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "InboxMessages";""");
        }
    }
}