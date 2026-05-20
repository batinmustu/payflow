using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Transaction.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "refunded_amount_minor",
                schema: "transaction",
                table: "transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refunded_amount_minor",
                schema: "transaction",
                table: "transactions");
        }
    }
}
