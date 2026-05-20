using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Transaction.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundProviderRefundReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_refund_reference",
                schema: "transaction",
                table: "refunds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider_refund_reference",
                schema: "transaction",
                table: "refunds");
        }
    }
}
