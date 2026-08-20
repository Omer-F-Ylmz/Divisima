using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class CumulativeRefundAndLoyaltyEarnUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "refunded_amount",
                table: "orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // ON KONTROL - SESSIZ VERI SILME YOK.
            // Filtreli UNIQUE indeks, gecmiste ciftlenmis kazanim satirlari varsa kurulamaz.
            // Bu satirlari otomatik SILMIYORUZ: silmek customers.loyalty_points bakiyesi ile defteri
            // AYIRIR (bakiye fazla kalir). Karar operatorundur; migration gurultulu sekilde durur ve
            // hangi siparislerin sorunlu oldugunu soyler.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM loyalty_transactions
    WHERE order_id IS NOT NULL AND [type] = 0
    GROUP BY order_id HAVING COUNT(*) > 1)
BEGIN
    DECLARE @siparisler NVARCHAR(2000) = (
        SELECT STRING_AGG(CAST(order_id AS NVARCHAR(20)), ',')
        FROM (SELECT order_id FROM loyalty_transactions
              WHERE order_id IS NOT NULL AND [type] = 0
              GROUP BY order_id HAVING COUNT(*) > 1) d);
    RAISERROR(N'Ciftlenmis sadakat kazanimi var - once mutabakat gerekli. Siparisler: %s', 16, 1, @siparisler);
END");

            migrationBuilder.CreateIndex(
                name: "UX_loyalty_transactions_order_earn",
                table: "loyalty_transactions",
                column: "order_id",
                unique: true,
                filter: "[order_id] IS NOT NULL AND [type] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_loyalty_transactions_order_earn",
                table: "loyalty_transactions");

            migrationBuilder.DropColumn(
                name: "refunded_amount",
                table: "orders");
        }
    }
}
