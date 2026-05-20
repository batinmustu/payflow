using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "report");

            migrationBuilder.CreateTable(
                name: "daily_transaction_summary",
                schema: "report",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    attempted_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    captured_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    captured_amount_minor = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    refunded_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    refunded_amount_minor = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_transaction_summary", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                schema: "report",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.message_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_daily_summary_tenant_date_desc",
                schema: "report",
                table: "daily_transaction_summary",
                columns: new[] { "tenant_id", "date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_daily_summary_tenant_date_currency",
                schema: "report",
                table: "daily_transaction_summary",
                columns: new[] { "tenant_id", "date", "currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_transaction_summary",
                schema: "report");

            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "report");
        }
    }
}
