using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Transaction.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundFinalProviderReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "final_provider_reference",
                schema: "transaction",
                table: "refunds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "final_provider_reference",
                schema: "transaction",
                table: "refunds");
        }
    }
}
