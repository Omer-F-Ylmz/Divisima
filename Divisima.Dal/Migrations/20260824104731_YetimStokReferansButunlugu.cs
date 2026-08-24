using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class YetimStokReferansButunlugu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ══ DALGA D / D2 - YETIM STOK SATIRLARI + REFERANS BUTUNLUGU ═══════════════════
            //
            // OLCULEN ONCE-DURUM (dev veritabani):
            //   yetim product_stocks satiri            : 120  (40 ayri product_id, 3..182)
            //   yetimde reserved_quantity > 0          : 0
            //   yetime bagli stock_reservations        : 0
            //   yetime bagli stock_movements           : 0
            //   yetime bagli order_items               : 0
            // Kaynak: Dalga 3'un performans seed temizligi urun satirlarini DOGRUDAN sildi,
            // stok satirlarini birakti. Uretim yolundan GELMEDI - ProductManager.Delete
            // SOFT-delete'tir (is_active=false), fiziksel silme yapan kod yolu YOK.
            //
            // SPRINT 6 KALIBI, IKI ADIMDA:
            //  1) ONCE KONTROL: bir yetim satirin BAGLI KAYDI varsa (rezerve adet, rezervasyon,
            //     stok hareketi ya da siparis kalemi) HICBIR SATIR SILINMEDEN gurultulu duser.
            //     Boyle bir satiri silmek, hala ona isaret eden bir gecmisi SESSIZCE yok etmek
            //     olurdu; hangi kaydin dogru oldugu karari operatorundur.
            //  2) SONRA TEMIZLIK: yalnizca ISPATLI SEKILDE ATIL olan yetimler silinir.
            // Sira onemli: once TUM yetimler taranir, sonra silinir - aksi halde yarim
            // temizlenmis bir durum kalabilirdi.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM product_stocks ps
    WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.id = ps.product_id)
      AND (
            ps.reserved_quantity > 0
         OR EXISTS (SELECT 1 FROM stock_reservations r WHERE r.product_id = ps.product_id)
         OR EXISTS (SELECT 1 FROM stock_movements  m WHERE m.product_id = ps.product_id)
         OR EXISTS (SELECT 1 FROM order_items      i WHERE i.product_id = ps.product_id)
      )
)
BEGIN
    RAISERROR (N'product_stocks tablosunda BAGLI KAYDI OLAN yetim satir(lar) var (rezerve adet / rezervasyon / stok hareketi / siparis kalemi). FK_product_stocks_product_id kurulamaz. Bu migration boyle satirlari SILMEZ - silmek hala onlara isaret eden gecmisi sessizce yok ederdi. Satirlar ELLE incelenmeli.', 16, 1);
END
");

            // ATIL YETIMLERI SIL. Kosul yukaridaki kontrolun TAM TERSI - yani buraya yalnizca
            // hicbir kaydin isaret etmedigi satirlar gelir.
            migrationBuilder.Sql(@"
DELETE ps
FROM product_stocks ps
WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.id = ps.product_id)
  AND ps.reserved_quantity = 0
  AND NOT EXISTS (SELECT 1 FROM stock_reservations r WHERE r.product_id = ps.product_id)
  AND NOT EXISTS (SELECT 1 FROM stock_movements  m WHERE m.product_id = ps.product_id)
  AND NOT EXISTS (SELECT 1 FROM order_items      i WHERE i.product_id = ps.product_id);
");

            // SILME DAVRANISI: Restrict = SQL Server'da ON DELETE NO ACTION. OLCUMLE SECILDI -
            // products'a isaret eden MEVCUT iki FK de (product_reviews, order_items) NO_ACTION,
            // yani deponun kendi konvansiyonu zaten "silmeyi ENGELLE".
            // CASCADE REDDEDILDI: uretimde silme SOFT oldugu icin cascade normal isleyiste HIC
            // atesLENMEZ; yalnizca dogrudan-SQL ile fiziksel silme durumunda atesLENIR ve tam da
            // durdurulmasi gereken anda stok gecmisini SESSIZCE goturur. Gerekcenin tamami
            // DivisimaDbContext'teki ProductStock yapilandirmasinda.
            //
            // ══ AD SEMA DOSYASIYLA HIZALANDI + VARLIK GUARD'I (DALGA ICI DENETIM BULGUSU) ═══
            // Denetimde olculdu: `database/mssql/01_schema.sql` (belgelenmis deploy varligi)
            // bu FK'yi ZATEN tanimliyor - satir 653, ad `FK_product_stocks_product_id`.
            // Yani kisit "yeni" degil; EKSIK OLAN EF tarafiydi. Iki sonuc:
            //  (1) AD, sema dosyasindakiyle AYNI secildi. EF'in urettigi varsayilan ad
            //      (`FK_product_stocks_products_product_id`) farkliydi; sema dosyasindan
            //      kurulmus bir veritabaninda migration IKINCI, GEREKSIZ bir kisit yaratirdi
            //      (SQL Server ayni kolonlarda mukerrer FK'ya izin verir - sessiz israf).
            //  (2) AddForeignKey yerine GUARD'LI ham SQL: kisit zaten varsa (sema dosyasindan
            //      kurulmus DB) atlanir, yoksa kurulur. Boylece IKI SAGLAMA YOLU DA ayni
            //      tek kisitta bulusur.
            // NOT: denetim daha genis bir ayrisma da olctu - sema dosyasi 55 FK / 35 tablo
            // tanimliyor, EF ile kurulan veritabaninda 11 FK / 10 tablo var. Bu kalem o
            // ayrismanin YALNIZCA bir satirini kapatir; genel karar kullanicinindir.
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_product_stocks_product_id'
      AND parent_object_id = OBJECT_ID('product_stocks')
)
BEGIN
    ALTER TABLE product_stocks
        ADD CONSTRAINT FK_product_stocks_product_id
        FOREIGN KEY (product_id) REFERENCES products(id);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Silinen yetim satirlar GERI GETIRILMEZ: hangi urune ait olduklari bilgisi zaten
            // kaybolmustu (urun satiri yoktu) ve hicbir kayit onlara isaret etmiyordu.
            // Guard'li dusurme: sema dosyasindan kurulmus bir DB'de kisit bu migration
            // TARAFINDAN olusturulmamis olabilir; yine de ayni ada sahip oldugu icin
            // dusurulmesi dogrudur (Up'in tersi).
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_product_stocks_product_id'
      AND parent_object_id = OBJECT_ID('product_stocks')
)
BEGIN
    ALTER TABLE product_stocks DROP CONSTRAINT FK_product_stocks_product_id;
END
");
        }
    }
}
