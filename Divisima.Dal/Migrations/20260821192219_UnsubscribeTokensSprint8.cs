using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class UnsubscribeTokensSprint8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unsubscribe_token",
                table: "stock_notification_requests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "unsubscribe_token",
                table: "price_drop_subscriptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // GERI DOLDURMA - INDEKSTEN ONCE ZORUNLU.
            // AddColumn tum mevcut satirlara AYNI varsayilani ("") yaziyor; UNIQUE indeks bu
            // yuzden tabloda IKI VEYA DAHA FAZLA satir varsa KURULAMAZ ve migration duser.
            // NEWID() SATIR BASINA yeniden degerlendirilir, dolayisiyla her satira farkli bir
            // deger yazar. Uzunluk 36 (<= 64) - kolon sinirina sigar.
            // Ayrica bos jeton birakmak istenmezdi: bos deger "gecerli bir jeton" gibi durur.
            // (UnsubscribeByToken bos jetonu zaten reddediyor, ama iki savunma iyidir.)
            migrationBuilder.Sql(
                "UPDATE stock_notification_requests SET unsubscribe_token = CONVERT(NVARCHAR(64), NEWID()) WHERE unsubscribe_token = N'';");
            migrationBuilder.Sql(
                "UPDATE price_drop_subscriptions SET unsubscribe_token = CONVERT(NVARCHAR(64), NEWID()) WHERE unsubscribe_token = N'';");

            migrationBuilder.CreateIndex(
                name: "UX_stock_notification_requests_token",
                table: "stock_notification_requests",
                column: "unsubscribe_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_price_drop_subscriptions_token",
                table: "price_drop_subscriptions",
                column: "unsubscribe_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_stock_notification_requests_token",
                table: "stock_notification_requests");

            migrationBuilder.DropIndex(
                name: "UX_price_drop_subscriptions_token",
                table: "price_drop_subscriptions");

            migrationBuilder.DropColumn(
                name: "unsubscribe_token",
                table: "stock_notification_requests");

            migrationBuilder.DropColumn(
                name: "unsubscribe_token",
                table: "price_drop_subscriptions");
        }
    }
}
