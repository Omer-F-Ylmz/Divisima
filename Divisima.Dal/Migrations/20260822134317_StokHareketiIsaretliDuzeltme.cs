using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <summary>
    /// DALGA-2-FIX (B11) - STOK HAREKET DEFTERINDE ISARET ONARIMI. VERI MIGRATION'I (sema DEGISMEZ).
    ///
    /// SORUN (Dalga 2'de olculdu): `StockManager.AdjustStock` hareket satirini
    /// `quantity = Math.Abs(delta)` ile yaziyordu. Yon YALNIZCA serbest metin `note` alanindaki
    /// "Admin duzeltme (-5)" ifadesinde duruyordu; sayisal defterde artis ile azalis AYIRT
    /// EDILEMIYORDU. Sonuc: defteri mutabakat eden biri urun 2 / M icin 18 buluyor,
    /// `product_stocks` 8 diyordu (hayali 10 fark).
    ///
    /// UYGULAMA TARAFI ZATEN DUZELTILDI (artik isaretli `delta` yaziliyor). Bu migration yalnizca
    /// MEVCUT satirlari ayni sozlesmeye tasir.
    ///
    /// SPRINT 6 KALIBIYLA - TAHMINLE ONARIM YOK:
    /// Isaret yalnizca notun URETILMIS ve BILINEN bicimi ("... duzeltme (+N): ..." / "(-N)")
    /// uzerinden okunur. Bu iki desenden HICBIRINE uymayan bir Adjustment satiri varsa
    /// HICBIR SATIR YAZILMADAN `RAISERROR` ile durulur - o satirin yonu BILINMIYOR demektir ve
    /// tahmin etmek defteri "onarmis gibi" yapip sessizce yanlislamak olurdu.
    ///
    /// COLLATION NOTU (bu depoda bir kez bedeli odendi - bkz. CLAUDE.md bolum 6c):
    /// veritabani `Turkish_CI_AS`. Desen eslesmesi `COLLATE Latin1_General_BIN2` ile yapilir ki
    /// harf katlamasi araya girmesin. Ayrica Turkce 'u' harfi desene HIC konmadi ("...zeltme (-")
    /// - boylece kodlama farkliliklarindan tamamen bagimsiz.
    ///
    /// In(1)/Out(2) satirlarina DOKUNULMAZ: onlarin yonu `movement_type` ile zaten belirli.
    /// </summary>
    public partial class StokHareketiIsaretliDuzeltme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1) ON KONTROL: yonu belirlenemeyen Adjustment satiri var mi? Varsa HICBIR SEY YAZMADAN dur.
DECLARE @belirsiz int = (
    SELECT COUNT(*) FROM stock_movements
    WHERE movement_type = 3
      AND note COLLATE Latin1_General_BIN2 NOT LIKE N'%zeltme (-%'
      AND note COLLATE Latin1_General_BIN2 NOT LIKE N'%zeltme (+%'
);
IF @belirsiz > 0
BEGIN
    DECLARE @m nvarchar(400) = N'B11 ISARET ONARIMI DURDURULDU: yonu notundan okunamayan '
        + CAST(@belirsiz AS nvarchar(20))
        + N' adet Adjustment satiri var. Bu satirlarin yonu BILINMIYOR ve TAHMIN EDILMEZ. '
        + N'Sorgu: SELECT id, quantity, note FROM stock_movements WHERE movement_type=3 '
        + N'AND note COLLATE Latin1_General_BIN2 NOT LIKE N''%zeltme (-%'' '
        + N'AND note COLLATE Latin1_General_BIN2 NOT LIKE N''%zeltme (+%'';';
    RAISERROR(@m, 16, 1);
    RETURN;
END

-- 2) AZALIS satirlarini negatife cevir. IDEMPOTENT: yalniz HALA POZITIF olanlar guncellenir,
--    yani migration yeniden kosarsa (or. elle) isaret ikinci kez ters cevrilmez.
UPDATE stock_movements
SET quantity = -quantity
WHERE movement_type = 3
  AND note COLLATE Latin1_General_BIN2 LIKE N'%zeltme (-%'
  AND quantity > 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: isareti kaldirip mutlak degere dondurur (migration ONCESI davranis).
            // NOT: bu, B11'in duzelttigi bilgi kaybini GERI GETIRIR - yalnizca surum geri alinirken
            // uygulama kodu da eski haline dondugu icin tutarli olsun diye var.
            migrationBuilder.Sql(@"
UPDATE stock_movements SET quantity = ABS(quantity) WHERE movement_type = 3 AND quantity < 0;
");
        }
    }
}
