using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class UcEksikReferans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === D-SEMA-FIX EKI: ESKI SEMA DOSYASININ ATLADIGI UC GERCEK ILISKI ==============
            //
            // D-SEMA olcumu, dosyanin yalniz FAZLA FK tanimlamadigini; GERCEK olan bazilarini da
            // ATLADIGINI gosterdi. Kok sebep yine kaldirilan uretec: FK'lari "<x>_id -> <x>s(id)"
            // ADLANDIRMA KURALINDAN cikariyordu, yani review_id icin olmayan bir "reviews"
            // tablosunu ariyor, bulamayinca SESSIZCE atliyordu. invoice_items hic kapsanmamisti.
            //
            //   invoice_items.invoice_id -> invoices.id          VERI KANITI VAR (27 satir, yetim 0)
            //   invoice_items.product_id -> products.id          VERI KANITI VAR (27 satir, yetim 0)
            //   review_helpful_votes.review_id -> product_reviews.id
            //       tablo dev'de BOS -> kanit YAZMA YOLUNDAN: ProductReviewManager.VoteHelpful
            //       yorumu ONCE arar, yoksa 404 doner; yani id dogrulanmis gelir.
            //
            // BILEREK EKLENMEYEN IKI ILISKI: products.seller_id ve order_items.seller_id.
            // Satici modulu KAPALI (Seller:RegistrationEnabled=false, sellers 0 satir) ve iki FK
            // modul acilirken G4 on kosuluyla BIRLIKTE eklenecek (bkz. CLAUDE.md / KARARLAR).
            //
            // ON KONTROL - SPRINT 6 KALIBI: kirli veri varsa HICBIR SATIR SILINMEDEN gurultulu
            // duser; hangi kaydin dogru oldugu karari OPERATORUNDUR.
            migrationBuilder.Sql(@"
DECLARE @ihlal TABLE (iliski NVARCHAR(200), adet INT);

INSERT INTO @ihlal SELECT N'invoice_items.invoice_id', COUNT(*) FROM [invoice_items] c WHERE c.[invoice_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoices] p WHERE p.[id] = c.[invoice_id]);
INSERT INTO @ihlal SELECT N'invoice_items.product_id', COUNT(*) FROM [invoice_items] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
INSERT INTO @ihlal SELECT N'review_helpful_votes.review_id', COUNT(*) FROM [review_helpful_votes] c WHERE c.[review_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [product_reviews] p WHERE p.[id] = c.[review_id]);

DELETE FROM @ihlal WHERE adet = 0;

IF EXISTS (SELECT 1 FROM @ihlal)
BEGIN
    DECLARE @liste NVARCHAR(1500) = N'';
    SELECT @liste = LEFT(@liste + iliski + N'=' + CAST(adet AS NVARCHAR(20)) + N'  ', 1500) FROM @ihlal;
    DECLARE @msg NVARCHAR(2048) =
        N'REFERANS BUTUNLUGU KURULAMAZ - YETIM SATIR(LAR) VAR: ' + @liste +
        N'| Bu migration SATIR SILMEZ. Her satir ELLE incelenmeli: ya ebeveyn kaydi geri '   +
        N'getirilmeli ya da cocuk satir bilincli olarak silinmeli. Karar operatorundur.';
    RAISERROR (@msg, 16, 1);
END
");
            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_product_id",
                table: "invoice_items",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id",
                principalTable: "invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_items_product_id",
                table: "invoice_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_review_helpful_votes_review_id",
                table: "review_helpful_votes",
                column: "review_id",
                principalTable: "product_reviews",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoice_items_invoice_id",
                table: "invoice_items");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_items_product_id",
                table: "invoice_items");

            migrationBuilder.DropForeignKey(
                name: "FK_review_helpful_votes_review_id",
                table: "review_helpful_votes");

            migrationBuilder.DropIndex(
                name: "IX_invoice_items_product_id",
                table: "invoice_items");
        }
    }
}
