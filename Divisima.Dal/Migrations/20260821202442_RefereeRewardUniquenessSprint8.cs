using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class RefereeRewardUniquenessSprint8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_store_credit_referee_reward",
                table: "store_credit_transactions",
                column: "customer_id",
                unique: true,
                filter: "[reason] = N'Referans ödülü (davet edilen)'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_store_credit_referee_reward",
                table: "store_credit_transactions");
        }
    }
}
