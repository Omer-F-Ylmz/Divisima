-- Divisima - Ornek/baslangic verisi (MSSQL)
-- Calistirma:  sqlcmd -S <sunucu> -d Divisima -i 02_seed.sql
-- NOT: Admin kullanicisi bu script'te DEGIL; uygulama acilisinda AdminSeeder olusturur
--      (AdminSeed config bolumu - HMACSHA512 hash uygulama tarafinda uretilir).
--      Musteri sifreleri de uygulama uzerinden kayitla olusur (SQL'de hash uretilemez).

SET NOCOUNT ON;
GO

-- === Kategoriler ===
SET IDENTITY_INSERT categories ON;
-- vat_rate: kalem bazli KDV. Uc kategori de GIYIM -> 0.1000 (%10). ACIKCA yazilir;
-- NULL birakilirsa efektif oran EInvoice:KdvRate'e (%20) duser ve giyim fazla KDV ile faturalanir.
-- Aksesuar kategorisi eklenirse 0.2000 ile olusturulmalidir.
INSERT INTO categories (id, name, slug, display_order, vat_rate, is_active, created_at) VALUES
  (1, N'Kadin Giyim', N'kadin-giyim', 1, 0.1000, 1, SYSUTCDATETIME()),
  (2, N'Elbise',      N'elbise',      2, 0.1000, 1, SYSUTCDATETIME()),
  (3, N'Dis Giyim',   N'dis-giyim',   3, 0.1000, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT categories OFF;
GO

-- === Urunler (fiyatlar KDV dahil) ===
-- average_rating / review_count: entity'de NOT NULL (varsayilan 0). Bu iki kolon seed'de
-- EKSIKTI ve INSERT "Cannot insert the value NULL into column 'average_rating'" ile SESSIZCE
-- dusuyordu - ardindan product_stocks FK'ya takiliyordu. Dosyanin sonundaki dogrulama blogu
-- artik bunu GURULTULU hale getiriyor (gerekce orada).
SET IDENTITY_INSERT products ON;
INSERT INTO products (id, name, brand, category_id, price, sale_price, description, color_hex, product_type, average_rating, review_count, is_active, created_at) VALUES
  (1, N'Siyah Elbise', N'Divisima', 2, 1200.00, NULL,    N'Zarif siyah midi elbise',    N'#000000', 0, 0, 0, 1, SYSUTCDATETIME()),
  (2, N'Kot Ceket',    N'Divisima', 3,  900.00, 720.00,  N'Oversize kot ceket',         N'#3B5998', 0, 0, 0, 1, SYSUTCDATETIME()),
  (3, N'Trenckot',     N'Divisima', 3, 2400.00, NULL,    N'Su gecirmez bej trenckot',   N'#C2B280', 0, 0, 0, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT products OFF;
GO

-- === Stok (beden bazli; stock_quantity + reserved_quantity) ===
SET IDENTITY_INSERT product_stocks ON;
INSERT INTO product_stocks (id, product_id, size, stock_quantity, reserved_quantity, is_active, created_at) VALUES
  (1, 1, N'S', 10, 0, 1, SYSUTCDATETIME()),
  (2, 1, N'M',  5, 0, 1, SYSUTCDATETIME()),
  (3, 1, N'L',  3, 0, 1, SYSUTCDATETIME()),
  (4, 2, N'M',  8, 0, 1, SYSUTCDATETIME()),
  (5, 3, N'L',  2, 0, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT product_stocks OFF;
GO

-- === Kuponlar ===
-- HOSGELDIN: %10, ilk siparise ozel, indirim tavani 100 TL
-- ESKIKOD:   suresi dolmus (test/ornek icin)
-- per_user_limit: entity'de NOT NULL, 0 = SINIRSIZ (Coupon.cs). Bu kolon da seed'de EKSIKTI.
-- Deger 0 secildi cunku entity varsayilani odur; HOSGELDIN zaten first_order_only=1 ile sinirli.
SET IDENTITY_INSERT coupons ON;
INSERT INTO coupons (id, code, discount_type, value, min_amount, max_discount_amount, expire_date, usage_limit, used_count, per_user_limit, first_order_only, is_active, created_at) VALUES
  (1, N'HOSGELDIN', 0, 10, 0,  100, '2030-01-01', 1000, 0, 0, 1, 1, SYSUTCDATETIME()),
  (2, N'ESKIKOD',   0, 20, 0, NULL, '2020-01-01', 1000, 0, 0, 0, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT coupons OFF;
GO

-- === Hediye karti ===
SET IDENTITY_INSERT gift_cards ON;
INSERT INTO gift_cards (id, code, initial_amount, balance, is_active, created_at) VALUES
  (1, N'GIFT-250', 250.00, 250.00, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT gift_cards OFF;
GO

-- === DOGRULAMA - YALAN SOYLEYEN "tamamlandi" MESAJI BIR KEZ BEDELINI ODEDI ===
-- Eski hali kosulsuz "Seed tamamlandi: 3 kategori, 3 urun, 5 stok..." basiyordu. Olculdu:
-- urun/stok/kupon INSERT'leri NOT NULL ihlaliyle dusuyor, kategoriler yaziliyor ve script
-- YINE DE bu satiri basip EXIT 0 ile bitiyordu - operator "basarili" goruyordu. (Ayni sinif
-- hata sema tarafinda da vardi; bkz. 01_schema.sql basligi.) Artik sayilar DOGRULANIR ve
-- tutmuyorsa script GURULTULU duser.
DECLARE @kategori INT = (SELECT COUNT(*) FROM categories);
DECLARE @urun     INT = (SELECT COUNT(*) FROM products);
DECLARE @stok     INT = (SELECT COUNT(*) FROM product_stocks);
DECLARE @kupon    INT = (SELECT COUNT(*) FROM coupons);
DECLARE @hediye   INT = (SELECT COUNT(*) FROM gift_cards);

IF @kategori <> 3 OR @urun <> 3 OR @stok <> 5 OR @kupon <> 2 OR @hediye <> 1
BEGIN
    DECLARE @m NVARCHAR(1000) =
        N'SEED EKSIK KALDI - beklenen 3/3/5/2/1, bulunan ' +
        CAST(@kategori AS NVARCHAR(10)) + N'/' + CAST(@urun AS NVARCHAR(10)) + N'/' +
        CAST(@stok AS NVARCHAR(10))     + N'/' + CAST(@kupon AS NVARCHAR(10)) + N'/' +
        CAST(@hediye AS NVARCHAR(10))   +
        N' (kategori/urun/stok/kupon/hediye). Yukaridaki Msg satirlarina bakin.';
    RAISERROR (@m, 16, 1);
END
ELSE
BEGIN
    PRINT 'Seed tamamlandi ve DOGRULANDI: 3 kategori, 3 urun, 5 stok, 2 kupon, 1 hediye karti.';
    PRINT 'Admin kullanicisi uygulama acilisinda AdminSeeder tarafindan olusturulacak.';
END
GO
