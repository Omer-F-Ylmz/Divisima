using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    // ══ KALITE SUPURMESI B1 - E-POSTA KANONIK BICIME NORMALIZE EDILIR ══════════════════════
    //
    // Uygulama tr-TR'ye pinlendikten sonra (Sprint 8 madde 13) e-postalar KULTURLU
    // `.ToLower()` ile saklaniyordu: 'I' -> 'ı' (U+0131). Veritabani collation'i Turkish_CI_AS
    // ve OLCULDU: 'irem' = 'IREM' -> FARKLI. Sonucu CANLI gorulmustu - ayni adresin iki
    // yazimi IKI AYRI HESAP acti ve kullanici ancak kayitta yazdigi harf duzeniyle giris
    // yapabiliyordu. Kod tarafi ToLowerInvariant'a cevrildi; bu migration MEVCUT satirlari
    // ayni kanonik bicime tasir.
    //
    // ── TASARIM: SESSIZ ONARIM YOK, GURULTULU DUSER (Sprint 6 kalibi) ────────────────────
    // Iki AYRI durum var ve ikisi ayni sey DEGIL:
    //
    //  (1) BUYUK HARFLI SATIR (or. 'Irem@X.com'). Guvenle duzeltilebilir: invariant kucultme
    //      yalnizca harf BUYUKLUGUNU degistirir, KARAKTERI degistirmez. NORMALIZE EDILIR.
    //      Once CAKISMA kontrolu yapilir: normalizasyon iki satiri ayni degere getirecekse
    //      HICBIR SEY YAZILMADAN RAISERROR ile durulur - IX_customers_email UNIQUE oldugu
    //      icin yazma zaten patlardi; anlasilir bir mesajla ONCE durmak yeglenir.
    //
    //  (2) TURKCE HASARLI SATIR (icinde 'ı' U+0131 ya da 'İ' U+0130 gecen). Bunlar
    //      OTOMATIK ONARILMAZ. Gerekce: onarim 'ı' -> 'i' KARAKTER DEGISIKLIGI demektir ve bu
    //      bir TAHMINDIR - adresin gercekten o karakteri icermedigini bilemeyiz. Kimlik
    //      verisinde tahminle yazmak, yanlis kisiye hesap acmak demek olabilir. Bu yuzden
    //      yalnizca GURULTULU sekilde RAPOR EDILIR; karar operatorun.
    //      (Olcum: bu dalgada yerel veritabaninda boyle TEK satir vardi ve o da bu supurmenin
    //       kendi sondaj hesabiydi - asagida siliniyor.)
    //
    // Down: normalize edilmis degerler GERI ALINAMAZ (orijinal harf buyuklugu bilgisi kayboldu)
    // ve zaten kanonik bicim DOGRU olandir. Down bilerek BOS - geri alinacak bir sema degisikligi
    // de yok.
    public partial class EmailKanonikNormalizasyon : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── SONDAJ ARTIGI TEMIZLIGI ──────────────────────────────────────────────────
            // Kalite supurmesi Dalga 1'de B1'i CANLI kanitlamak icin acilan iki hesap.
            // Test artigidir; kullanici karariyla siliniyor. Yalniz TAM ESLESEN adresler ve
            // yalniz bagli kaydi OLMAYANLAR silinir - baska bir veriye dokunmaz.
            migrationBuilder.Sql(@"
DELETE FROM customers
WHERE email COLLATE Latin1_General_BIN2 IN (
        N'iris.kalite@example.com' COLLATE Latin1_General_BIN2,
        N'' + NCHAR(305) + N'ris.kalite@example.com' COLLATE Latin1_General_BIN2)
  AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = customers.id);
");

            // ── (2) TURKCE HASARLI SATIRLAR: RAPOR ET, ONARMA ────────────────────────────
            // NOT: LIKE karsilastirmasi IKILI collation ile yapilir. Veritabani collation'i
            // Turkish_CI_AS oldugu icin duz bir LIKE N'%ı%' ifadesi 'i' iceren HER satiri de
            // yakalardi (bu dalgada teshis sorgusunda birebir yasandi) - yani hasarsiz satirlari
            // hasarli sanip gurultu uretirdi.
            migrationBuilder.Sql(@"
DECLARE @hasarli INT = (
    SELECT COUNT(*) FROM customers
    WHERE email COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(305) + N'%' COLLATE Latin1_General_BIN2
       OR email COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(304) + N'%' COLLATE Latin1_General_BIN2);
IF @hasarli > 0
    RAISERROR(N'B1 UYARI: %d musteri e-postasi Turkce kucultme ile HASARLI (ici ''i'' yerine ''i'' ya da ''I'' iceriyor). OTOMATIK ONARILMADI - karakter degisikligi TAHMIN olur. Bu satirlari elle inceleyin: SELECT id, email FROM customers WHERE email COLLATE Latin1_General_BIN2 LIKE N''%%'' + NCHAR(305) + N''%%'' COLLATE Latin1_General_BIN2 OR email COLLATE Latin1_General_BIN2 LIKE N''%%'' + NCHAR(304) + N''%%'' COLLATE Latin1_General_BIN2;', 16, 1, @hasarli);
");

            // ── (1) CAKISMA ON KONTROLU: yazmadan ONCE dur ───────────────────────────────
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM customers
    GROUP BY LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2
    HAVING COUNT(*) > 1)
    RAISERROR(N'B1 DURDURULDU: e-postalari kanonik bicime normalize etmek IKI VEYA DAHA FAZLA satiri ayni degere getirecek (IX_customers_email UNIQUE). Hicbir satir DEGISTIRILMEDI. Cakisan gruplari inceleyip mukerrer hesaplari birlestirin: SELECT LOWER(email COLLATE Latin1_General_CI_AS) AS kanonik, COUNT(*) FROM customers GROUP BY LOWER(email COLLATE Latin1_General_CI_AS) HAVING COUNT(*) > 1;', 16, 1);
");

            // ── (1) NORMALIZASYON: yalnizca harf buyuklugu degisir ───────────────────────
            migrationBuilder.Sql(@"
UPDATE customers
SET email = LOWER(email COLLATE Latin1_General_CI_AS)
WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;
");

            // Satici tablosu ayni kok ilkeye tabi (bugun veri duzeyinde kapali, 0 satir).
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'sellers')
    UPDATE sellers
    SET email = LOWER(email COLLATE Latin1_General_CI_AS)
    WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;
");

            // ── B2: KUPON KODLARI KANONIK BICIME (ToUpperInvariant karsiligi) ────────────
            migrationBuilder.Sql(@"
UPDATE coupons
SET code = UPPER(code COLLATE Latin1_General_CI_AS)
WHERE code COLLATE Latin1_General_BIN2 <> UPPER(code COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;
");

            // ── B1: ABONELIK E-POSTALARI (sahiplik anahtari) ─────────────────────────────
            migrationBuilder.Sql(@"
UPDATE stock_notification_requests
SET email = LOWER(email COLLATE Latin1_General_CI_AS)
WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;

UPDATE price_drop_subscriptions
SET email = LOWER(email COLLATE Latin1_General_CI_AS)
WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bilerek BOS: sema degisikligi yok ve kanonik bicim DOGRU olandir.
            // Orijinal harf buyuklugu bilgisi geri getirilemez.
        }
    }
}
