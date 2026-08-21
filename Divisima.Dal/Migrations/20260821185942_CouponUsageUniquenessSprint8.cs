using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class CouponUsageUniquenessSprint8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SPRINT 8 MADDE 1 - ON KONTROL (Sprint 6'daki sadakat indeksiyle AYNI kalip).
            // Kirli bir veritabaninda ayni (coupon_id, order_id) icin birden fazla kullanim
            // satiri varsa indeks kurulamaz. SATIR SILMIYORUZ: silmek `coupons.used_count` ile
            // defteri ayirir ve hangi satirin dogru oldugu kararini SESSIZCE bizim yerimize
            // vermis oluruz. Migration GURULTULU duser; mutabakat karari operatorundur.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM coupon_usages
    GROUP BY coupon_id, order_id
    HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR (N'coupon_usages tablosunda ayni (coupon_id, order_id) icin BIRDEN FAZLA satir var. UX_coupon_usages_coupon_order kurulamaz. Fazla satirlar ELLE incelenmeli - bu migration satir SILMEZ (silmek coupons.used_count ile defteri ayirirdi).', 16, 1);
END
");

            migrationBuilder.CreateIndex(
                name: "UX_coupon_usages_coupon_order",
                table: "coupon_usages",
                columns: new[] { "coupon_id", "order_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_coupon_usages_coupon_order",
                table: "coupon_usages");
        }
    }
}
