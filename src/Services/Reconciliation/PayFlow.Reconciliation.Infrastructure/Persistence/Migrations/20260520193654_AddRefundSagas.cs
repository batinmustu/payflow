using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Reconciliation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundSagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refund_sagas",
                schema: "reconciliation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    provider_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    provider_refund_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_sagas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refund_sagas_tenant_started_desc",
                schema: "reconciliation",
                table: "refund_sagas",
                columns: new[] { "tenant_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_refund_sagas_tenant_refund",
                schema: "reconciliation",
                table: "refund_sagas",
                columns: new[] { "tenant_id", "refund_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refund_sagas",
                schema: "reconciliation");
        }
    }
}
