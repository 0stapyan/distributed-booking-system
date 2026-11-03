using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    public partial class AddOutboxMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "OutboxMessages" (
                    "Id" uuid NOT NULL,
                    "EventType" text NOT NULL,
                    "Payload" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "PublishedAt" timestamp with time zone NULL,
                    "RetryCount" integer NOT NULL DEFAULT 0,
                    "TraceParent" text NULL,
                    CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OutboxMessages");
        }
    }
}