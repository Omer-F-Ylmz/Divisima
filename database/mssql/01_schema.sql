-- =====================================================================================
-- URETILMIS DOSYA - ELLE DUZENLEMEYIN
-- =====================================================================================
-- Bu dosya EF Core migration'larindan URETILIR. Tek dogruluk kaynagi Divisima.Dal/Migrations
-- klasorudur; burasi yalnizca onun DAGITILABILIR CIKTISIDIR (.NET araci olmayan bir ortamda
-- semayi kurabilmek icin). Elle yapilan her degisiklik bir sonraki uretimde KAYBOLUR.
--
-- YENIDEN URETMEK ICIN:
--   dotnet ef migrations script --idempotent \
--     --project Divisima.Dal --startup-project Divisima.API --context DivisimaDbContext \
--     -o database/mssql/01_schema.sql
--   (ardindan basina bu baslik blogu yeniden konur)
--
-- UYGULAMAK ICIN (IKI BAYRAK DA ZORUNLU):
--   sqlcmd -S <sunucu> -d Divisima -b -f 65001 -i database/mssql/01_schema.sql
--   sqlcmd -S <sunucu> -d Divisima -b -f 65001 -i database/mssql/02_seed.sql
--
--   -b        : bir ifade patlarsa sqlcmd SIFIR DISI kod dondursun.
--   -f 65001  : dosya UTF-8; kod sayfasi verilmezse Turkce karakterler BOZULUR.
--
-- IKI BAYRAK NEDEN ZORUNLU - BEDELI OLCULDU (D-SEMA):
--   Bu dosyanin ONCEKI hali entity siniflarindan uretilen ELLE BAKIMLI bir script'ti ve
--   55 FK beyan ediyordu. Dokumandaki komutta -b YOKTU ve dosyada GO YOKTU; satir 635'teki
--   gecersiz bir FK (orders.payment_id -> payments.id; ilki NVARCHAR, ikincisi INT) patlayip
--   BATCH'I DUSURUYOR, sonrasindaki 37 FK ve 65 indeks HIC olusmuyordu. sqlcmd yine de
--   EXIT 0 donuyordu, yani operator "basarili" goruyordu. OLCULEN SONUC: 17/55 FK, 6/71 indeks.
--   Ayrica -f 65001 verilmediginde UX_store_credit_referee_reward filtresindeki Turkce metin
--   bozuluyor ve indeks HICBIR SATIRLA eslesmiyordu - varlik gorunur, koruma YOK.
--   EF'in urettigi bu script GO batch'li oldugu icin birinci tuzak yapisal olarak yok;
--   ikincisi icin -f 65001 ZORUNLUDUR.
--
-- SIRA: sema (bu dosya) -> 02_seed.sql -> uygulama acilisi (AdminSeeder ilk admini olusturur).
-- Uygulama ACILISTA MIGRATE ETMEZ; sema kurulumu AYRI ve AYRICALIKLI bir adimdir
-- (bkz. ops/deployment-checklist.md - "Veritabani semasi").
-- =====================================================================================
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [audit_logs] (
        [id] int NOT NULL IDENTITY,
        [table_name] nvarchar(100) NOT NULL,
        [entity_id] nvarchar(60) NOT NULL,
        [action] nvarchar(20) NOT NULL,
        [changes] nvarchar(max) NULL,
        [user_id] nvarchar(60) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_audit_logs] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [categories] (
        [id] int NOT NULL IDENTITY,
        [name] nvarchar(100) NOT NULL,
        [slug] nvarchar(100) NOT NULL,
        [display_order] int NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_categories] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [collection_items] (
        [id] int NOT NULL IDENTITY,
        [collection_id] int NOT NULL,
        [product_id] int NOT NULL,
        [display_order] int NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_collection_items] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [collections] (
        [id] int NOT NULL IDENTITY,
        [name] nvarchar(150) NOT NULL,
        [slug] nvarchar(150) NOT NULL,
        [collection_type] tinyint NOT NULL,
        [curator_name] nvarchar(120) NULL,
        [subtitle] nvarchar(300) NULL,
        [gradient] nvarchar(200) NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_collections] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [consent_records] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NULL,
        [consent_type] nvarchar(40) NOT NULL,
        [document_version] nvarchar(40) NOT NULL,
        [granted] bit NOT NULL,
        [ip_address] nvarchar(max) NULL,
        [user_agent] nvarchar(max) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_consent_records] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [contents] (
        [id] int NOT NULL IDENTITY,
        [slug] nvarchar(100) NOT NULL,
        [title_tr] nvarchar(200) NOT NULL,
        [title_en] nvarchar(200) NULL,
        [body_tr] nvarchar(max) NOT NULL,
        [body_en] nvarchar(max) NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_contents] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [coupon_usages] (
        [id] int NOT NULL IDENTITY,
        [coupon_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [order_id] int NOT NULL,
        [discount_applied] decimal(18,2) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_coupon_usages] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [coupons] (
        [id] int NOT NULL IDENTITY,
        [code] nvarchar(40) NOT NULL,
        [discount_type] tinyint NOT NULL,
        [value] decimal(18,2) NOT NULL,
        [min_amount] decimal(18,2) NOT NULL,
        [max_discount_amount] decimal(18,2) NULL,
        [expire_date] datetime2 NULL,
        [usage_limit] int NOT NULL,
        [per_user_limit] int NOT NULL,
        [used_count] int NOT NULL,
        [first_order_only] bit NOT NULL DEFAULT CAST(0 AS bit),
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        [row_version] rowversion NOT NULL,
        CONSTRAINT [PK_coupons] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [customer_devices] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [device_token] nvarchar(500) NOT NULL,
        [platform] tinyint NOT NULL,
        [is_active] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [last_used_at] datetime2 NULL,
        CONSTRAINT [PK_customer_devices] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [customers] (
        [id] int NOT NULL IDENTITY,
        [name] nvarchar(max) NOT NULL,
        [email] nvarchar(200) NOT NULL,
        [user_type] tinyint NOT NULL DEFAULT CAST(2 AS tinyint),
        [phone] nvarchar(20) NOT NULL,
        [address] nvarchar(max) NULL,
        [city] nvarchar(max) NULL,
        [gender] int NULL,
        [password_salt] varbinary(max) NOT NULL,
        [password_hash] varbinary(max) NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        [last_login_at] datetime2 NULL,
        [email_verified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [email_verification_token] nvarchar(120) NULL,
        [email_verification_sent_at] datetime2 NULL,
        [password_reset_token] nvarchar(120) NULL,
        [password_reset_expiry] datetime2 NULL,
        [two_factor_enabled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [two_factor_secret] nvarchar(400) NULL,
        [two_factor_code] nvarchar(max) NULL,
        [two_factor_code_expiry] datetime2 NULL,
        [failed_login_attempts] int NOT NULL,
        [lockout_end] datetime2 NULL,
        [birthdate] datetime2 NULL,
        [notify_email] bit NOT NULL DEFAULT CAST(1 AS bit),
        [notify_sms] bit NOT NULL DEFAULT CAST(1 AS bit),
        [notify_push] bit NOT NULL DEFAULT CAST(1 AS bit),
        [loyalty_points] int NOT NULL DEFAULT 0,
        [store_credit] decimal(18,2) NOT NULL DEFAULT 0.0,
        [referral_code] nvarchar(20) NULL,
        [referred_by] int NULL,
        [last_order_at] datetime2 NULL,
        [last_winback_sent_at] datetime2 NULL,
        [birthday_offer_sent_year] datetime2 NULL,
        CONSTRAINT [PK_customers] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [gift_cards] (
        [id] int NOT NULL IDENTITY,
        [code] nvarchar(20) NOT NULL,
        [initial_amount] decimal(18,2) NOT NULL,
        [balance] decimal(18,2) NOT NULL,
        [is_active] bit NOT NULL,
        [redeemed_by] int NULL,
        [created_at] datetime2 NOT NULL,
        [redeemed_at] datetime2 NULL,
        CONSTRAINT [PK_gift_cards] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [invoices] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [invoice_number] nvarchar(40) NOT NULL,
        [invoice_type] tinyint NOT NULL,
        [tax_number] nvarchar(max) NULL,
        [company_name] nvarchar(max) NULL,
        [subtotal] decimal(18,2) NOT NULL,
        [tax_rate] decimal(5,4) NOT NULL,
        [tax_amount] decimal(18,2) NOT NULL,
        [total] decimal(18,2) NOT NULL,
        [status] tinyint NOT NULL,
        [provider_invoice_id] nvarchar(max) NULL,
        [pdf_url] nvarchar(300) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_invoices] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [loyalty_transactions] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [points] int NOT NULL,
        [type] tinyint NOT NULL,
        [reason] nvarchar(200) NOT NULL,
        [order_id] int NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_loyalty_transactions] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [order_snapshot_items] (
        [id] int NOT NULL IDENTITY,
        [order_snapshot_id] int NOT NULL,
        [product_id] int NOT NULL,
        [product_name] nvarchar(200) NOT NULL,
        [brand] nvarchar(120) NOT NULL,
        [product_price] decimal(18,2) NOT NULL,
        [size] nvarchar(10) NOT NULL,
        [quantity] int NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_order_snapshot_items] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [order_snapshots] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [customer_full_name] nvarchar(200) NOT NULL,
        [shipping_address] nvarchar(500) NULL,
        [status] tinyint NOT NULL,
        [subtotal] decimal(18,2) NOT NULL,
        [discount_amount] decimal(18,2) NOT NULL,
        [shipping_cost] decimal(18,2) NOT NULL,
        [total_price] decimal(18,2) NOT NULL,
        [coupon_code] nvarchar(40) NULL,
        [snapshot_created_at] datetime2 NOT NULL,
        [order_created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_order_snapshots] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [order_status_histories] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [status] tinyint NOT NULL,
        [note] nvarchar(500) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_order_status_histories] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [outbox_messages] (
        [id] int NOT NULL IDENTITY,
        [event_type] nvarchar(100) NOT NULL,
        [payload] nvarchar(max) NOT NULL,
        [status] tinyint NOT NULL,
        [retry_count] int NOT NULL,
        [error] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL,
        [processed_at] datetime2 NULL,
        CONSTRAINT [PK_outbox_messages] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [payments] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [payment_provider] nvarchar(40) NOT NULL,
        [payment_status] tinyint NOT NULL,
        [amount] decimal(18,2) NOT NULL,
        [paid_price] decimal(18,2) NULL,
        [installment_count] tinyint NOT NULL,
        [installment_fee] decimal(18,2) NULL,
        [currency] nvarchar(10) NULL,
        [fraud_status] nvarchar(10) NULL,
        [transaction_id] nvarchar(120) NULL,
        [conversation_id] nvarchar(120) NULL,
        [token] nvarchar(120) NULL,
        [paid_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_payments] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [price_drop_subscriptions] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [email] nvarchar(256) NOT NULL,
        [subscribed_price] decimal(18,2) NOT NULL,
        [is_notified] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [notified_at] datetime2 NULL,
        CONSTRAINT [PK_price_drop_subscriptions] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [product_attributes] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [attribute_key] nvarchar(50) NOT NULL,
        [attribute_value] nvarchar(100) NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_product_attributes] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [product_images] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [image_url] nvarchar(1000) NOT NULL,
        [sort_order] int NOT NULL,
        [is_primary] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_product_images] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [product_questions] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [question] nvarchar(1000) NOT NULL,
        [answer] nvarchar(2000) NULL,
        [answered_by] int NULL,
        [is_answered] bit NOT NULL,
        [is_active] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [answered_at] datetime2 NULL,
        CONSTRAINT [PK_product_questions] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [product_stocks] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [size] nvarchar(10) NOT NULL,
        [stock_quantity] int NOT NULL,
        [reserved_quantity] int NOT NULL DEFAULT 0,
        [row_version] rowversion NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_product_stocks] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [products] (
        [id] int NOT NULL IDENTITY,
        [name] nvarchar(200) NOT NULL,
        [brand] nvarchar(120) NOT NULL,
        [category_id] int NOT NULL,
        [sub_category_id] int NULL,
        [price] decimal(18,2) NOT NULL,
        [sale_price] decimal(18,2) NULL,
        [sale_start] datetime2 NULL,
        [sale_end] datetime2 NULL,
        [old_price] decimal(18,2) NULL,
        [description] nvarchar(max) NOT NULL,
        [color_hex] nvarchar(9) NOT NULL,
        [variant_group_id] nvarchar(50) NULL,
        [image_url] nvarchar(1000) NULL,
        [product_type] tinyint NOT NULL,
        [average_rating] decimal(18,2) NOT NULL,
        [review_count] int NOT NULL,
        [seller_id] int NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_products] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [recently_viewed_products] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [product_id] int NOT NULL,
        [viewed_at] datetime2 NOT NULL,
        CONSTRAINT [PK_recently_viewed_products] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [return_requests] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [product_id] int NOT NULL,
        [size] nvarchar(20) NOT NULL,
        [quantity] int NOT NULL,
        [reason] tinyint NOT NULL,
        [description] nvarchar(1000) NULL,
        [return_type] tinyint NOT NULL,
        [status] tinyint NOT NULL,
        [refund_amount] decimal(18,2) NOT NULL,
        [refund_id] nvarchar(120) NULL,
        [admin_note] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        [processed_at] datetime2 NULL,
        CONSTRAINT [PK_return_requests] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [review_helpful_votes] (
        [id] int NOT NULL IDENTITY,
        [review_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_review_helpful_votes] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [security_events] (
        [id] int NOT NULL IDENTITY,
        [event_type] nvarchar(60) NOT NULL,
        [severity] nvarchar(20) NOT NULL,
        [customer_id] int NULL,
        [ip_address] nvarchar(60) NULL,
        [user_agent] nvarchar(300) NULL,
        [detail] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_security_events] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [sellers] (
        [id] int NOT NULL IDENTITY,
        [business_name] nvarchar(200) NOT NULL,
        [email] nvarchar(200) NOT NULL,
        [user_type] tinyint NOT NULL DEFAULT CAST(3 AS tinyint),
        [password_salt] varbinary(max) NOT NULL,
        [password_hash] varbinary(max) NOT NULL,
        [phone] nvarchar(20) NOT NULL,
        [tax_number] nvarchar(30) NULL,
        [status] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
        [commission_rate] decimal(5,2) NOT NULL DEFAULT 10.0,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [failed_login_attempts] int NOT NULL,
        [lockout_end] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_sellers] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [shipments] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [carrier] tinyint NOT NULL,
        [tracking_number] nvarchar(100) NOT NULL,
        [status] tinyint NOT NULL,
        [last_status_text] nvarchar(300) NULL,
        [shipped_at] datetime2 NULL,
        [estimated_delivery] datetime2 NULL,
        [delivered_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [last_checked_at] datetime2 NULL,
        CONSTRAINT [PK_shipments] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [size_guide_entries] (
        [id] int NOT NULL IDENTITY,
        [category_id] int NOT NULL,
        [size_label] nvarchar(20) NOT NULL,
        [bust_cm] decimal(6,2) NULL,
        [waist_cm] decimal(6,2) NULL,
        [hip_cm] decimal(6,2) NULL,
        [length_cm] decimal(6,2) NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [sort_order] int NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_size_guide_entries] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [stock_movements] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [size] nvarchar(10) NOT NULL,
        [movement_type] tinyint NOT NULL,
        [quantity] int NOT NULL,
        [reference_id] int NULL,
        [note] nvarchar(200) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_stock_movements] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [stock_notification_requests] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [size] nvarchar(20) NOT NULL,
        [email] nvarchar(256) NOT NULL,
        [is_notified] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [notified_at] datetime2 NULL,
        CONSTRAINT [PK_stock_notification_requests] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [stock_reservations] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [product_id] int NOT NULL,
        [size] nvarchar(20) NOT NULL,
        [quantity] int NOT NULL,
        [status] tinyint NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [created_at] datetime2 NOT NULL,
        [closed_at] datetime2 NULL,
        CONSTRAINT [PK_stock_reservations] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [store_credit_transactions] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [amount] decimal(18,2) NOT NULL,
        [type] tinyint NOT NULL,
        [reason] nvarchar(200) NOT NULL,
        [order_id] int NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_store_credit_transactions] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [sub_categories] (
        [id] int NOT NULL IDENTITY,
        [category_id] int NOT NULL,
        [name] nvarchar(100) NOT NULL,
        [slug] nvarchar(100) NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_sub_categories] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [user_sessions] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [refresh_token] nvarchar(500) NOT NULL,
        [device] nvarchar(200) NULL,
        [ip_address] nvarchar(64) NULL,
        [expires_at] datetime2 NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_user_sessions] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [addresses] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [title] nvarchar(60) NOT NULL,
        [full_name] nvarchar(150) NOT NULL,
        [phone] nvarchar(20) NOT NULL,
        [city] nvarchar(60) NOT NULL,
        [district] nvarchar(60) NOT NULL,
        [full_address] nvarchar(500) NOT NULL,
        [zip_code] nvarchar(20) NULL,
        [is_default] bit NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_addresses] PRIMARY KEY ([id]),
        CONSTRAINT [FK_addresses_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [carts] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        [reminder_sent_at] datetime2 NULL,
        CONSTRAINT [PK_carts] PRIMARY KEY ([id]),
        CONSTRAINT [FK_carts_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [orders] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [order_number] nvarchar(40) NOT NULL,
        [request_id] nvarchar(80) NULL,
        [status] tinyint NOT NULL,
        [subtotal] decimal(18,2) NOT NULL,
        [discount_amount] decimal(18,2) NOT NULL,
        [shipping_cost] decimal(18,2) NOT NULL,
        [total_price] decimal(18,2) NOT NULL,
        [currency] nvarchar(10) NOT NULL DEFAULT N'TRY',
        [coupon_code] nvarchar(40) NULL,
        [address_id] int NULL,
        [payment_type] tinyint NOT NULL,
        [store_credit_used] decimal(18,2) NOT NULL,
        [installment_count] tinyint NOT NULL,
        [is_online_payment_done] bit NOT NULL,
        [payment_id] nvarchar(120) NULL,
        [created_at] datetime2 NOT NULL,
        [delivered_at] datetime2 NULL,
        [review_invite_sent_at] datetime2 NULL,
        CONSTRAINT [PK_orders] PRIMARY KEY ([id]),
        CONSTRAINT [FK_orders_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [wishlist_items] (
        [id] int NOT NULL IDENTITY,
        [customer_id] int NOT NULL,
        [product_id] int NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_wishlist_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_wishlist_items_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [product_reviews] (
        [id] int NOT NULL IDENTITY,
        [product_id] int NOT NULL,
        [customer_id] int NOT NULL,
        [rating] int NOT NULL,
        [comment] nvarchar(1000) NOT NULL,
        [is_verified_purchase] bit NOT NULL DEFAULT CAST(0 AS bit),
        [helpful_count] int NOT NULL DEFAULT 0,
        [review_status] tinyint NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_product_reviews] PRIMARY KEY ([id]),
        CONSTRAINT [FK_product_reviews_products_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [cart_items] (
        [id] int NOT NULL IDENTITY,
        [cart_id] int NOT NULL,
        [product_id] int NOT NULL,
        [size] nvarchar(10) NOT NULL,
        [quantity] int NOT NULL,
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_cart_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_cart_items_carts_cart_id] FOREIGN KEY ([cart_id]) REFERENCES [carts] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE TABLE [order_items] (
        [id] int NOT NULL IDENTITY,
        [order_id] int NOT NULL,
        [product_id] int NOT NULL,
        [size] nvarchar(10) NOT NULL,
        [quantity] int NOT NULL,
        [unit_price] decimal(18,2) NOT NULL,
        [seller_id] int NULL,
        [is_cancelled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_order_items] PRIMARY KEY ([id]),
        CONSTRAINT [FK_order_items_orders_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_order_items_products_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_addresses_customer_id] ON [addresses] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_audit_logs_table_name_created_at] ON [audit_logs] ([table_name], [created_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_cart_items_cart_id] ON [cart_items] ([cart_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_cart_items_cart_id_product_id_size] ON [cart_items] ([cart_id], [product_id], [size]) WHERE [is_active] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_carts_customer_id] ON [carts] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_categories_slug] ON [categories] ([slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_collection_items_collection_id_product_id] ON [collection_items] ([collection_id], [product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_collections_slug] ON [collections] ([slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_consent_records_customer_id_consent_type] ON [consent_records] ([customer_id], [consent_type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_contents_slug] ON [contents] ([slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_coupons_code] ON [coupons] ([code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_customer_devices_customer_id] ON [customer_devices] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_customer_devices_device_token] ON [customer_devices] ([device_token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_customers_email] ON [customers] ([email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_customers_referral_code] ON [customers] ([referral_code]) WHERE [referral_code] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_gift_cards_code] ON [gift_cards] ([code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoices_customer_id] ON [invoices] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_invoices_invoice_number] ON [invoices] ([invoice_number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_invoices_order_id] ON [invoices] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_loyalty_transactions_customer_id_created_at] ON [loyalty_transactions] ([customer_id], [created_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_order_id] ON [order_items] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_product_id] ON [order_items] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_seller_id] ON [order_items] ([seller_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_status_histories_order_id] ON [order_status_histories] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_customer_id] ON [orders] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_customer_id_created_at] ON [orders] ([customer_id], [created_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_orders_order_number] ON [orders] ([order_number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_orders_request_id] ON [orders] ([request_id]) WHERE [request_id] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_outbox_messages_status_created_at] ON [outbox_messages] ([status], [created_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_conversation_id] ON [payments] ([conversation_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_order_id] ON [payments] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_token] ON [payments] ([token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_price_drop_subscriptions_product_id_email] ON [price_drop_subscriptions] ([product_id], [email]) WHERE [is_notified] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_price_drop_subscriptions_product_id_is_notified] ON [price_drop_subscriptions] ([product_id], [is_notified]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_product_attributes_attribute_key_attribute_value] ON [product_attributes] ([attribute_key], [attribute_value]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_product_attributes_product_id] ON [product_attributes] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_product_images_product_id] ON [product_images] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_product_questions_product_id_is_answered] ON [product_questions] ([product_id], [is_answered]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_product_reviews_customer_id_product_id] ON [product_reviews] ([customer_id], [product_id]) WHERE [is_active] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_product_reviews_product_id] ON [product_reviews] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_product_reviews_product_id_review_status] ON [product_reviews] ([product_id], [review_status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_product_stocks_product_id_size] ON [product_stocks] ([product_id], [size]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_products_category_id] ON [products] ([category_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_products_is_active] ON [products] ([is_active]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_products_seller_id] ON [products] ([seller_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_products_variant_group_id] ON [products] ([variant_group_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_recently_viewed_products_customer_id_product_id] ON [recently_viewed_products] ([customer_id], [product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_recently_viewed_products_customer_id_viewed_at] ON [recently_viewed_products] ([customer_id], [viewed_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_return_requests_customer_id] ON [return_requests] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_return_requests_order_id] ON [return_requests] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_return_requests_status] ON [return_requests] ([status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_review_helpful_votes_review_id_customer_id] ON [review_helpful_votes] ([review_id], [customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_security_events_customer_id] ON [security_events] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_security_events_event_type_created_at] ON [security_events] ([event_type], [created_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_sellers_email] ON [sellers] ([email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_sellers_status] ON [sellers] ([status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_shipments_order_id] ON [shipments] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_shipments_tracking_number] ON [shipments] ([tracking_number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_size_guide_entries_category_id] ON [size_guide_entries] ([category_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_stock_movements_product_id_size] ON [stock_movements] ([product_id], [size]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_stock_notification_requests_product_id_size_email] ON [stock_notification_requests] ([product_id], [size], [email]) WHERE [is_notified] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_stock_notification_requests_product_id_size_is_notified] ON [stock_notification_requests] ([product_id], [size], [is_notified]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_stock_reservations_order_id] ON [stock_reservations] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_stock_reservations_status_expires_at] ON [stock_reservations] ([status], [expires_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_store_credit_transactions_customer_id_created_at] ON [store_credit_transactions] ([customer_id], [created_at]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_sub_categories_category_id_slug] ON [sub_categories] ([category_id], [slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_sessions_customer_id] ON [user_sessions] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_sessions_refresh_token] ON [user_sessions] ([refresh_token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_wishlist_items_customer_id_product_id] ON [wishlist_items] ([customer_id], [product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722165623_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722165623_InitialCreate', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    ALTER TABLE [products] ADD [vat_rate] decimal(5,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[customers]') AND [c].[name] = N'phone');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [customers] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [customers] ALTER COLUMN [phone] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    ALTER TABLE [categories] ADD [vat_rate] decimal(5,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[addresses]') AND [c].[name] = N'phone');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [addresses] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [addresses] ALTER COLUMN [phone] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    CREATE TABLE [invoice_items] (
        [id] int NOT NULL IDENTITY,
        [invoice_id] int NOT NULL,
        [product_id] int NOT NULL,
        [product_name] nvarchar(200) NOT NULL,
        [quantity] int NOT NULL,
        [unit_price] decimal(18,2) NOT NULL,
        [line_subtotal] decimal(18,2) NOT NULL,
        [vat_rate] decimal(5,4) NOT NULL,
        [vat_amount] decimal(18,2) NOT NULL,
        [line_total] decimal(18,2) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_invoice_items] PRIMARY KEY ([id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    CREATE INDEX [IX_invoice_items_invoice_id] ON [invoice_items] ([invoice_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    UPDATE categories SET vat_rate = 0.1000 WHERE vat_rate IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820125737_LineItemVatAndNullablePhone'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260820125737_LineItemVatAndNullablePhone', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820141003_CumulativeRefundAndLoyaltyEarnUniqueness'
)
BEGIN
    ALTER TABLE [orders] ADD [refunded_amount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820141003_CumulativeRefundAndLoyaltyEarnUniqueness'
)
BEGIN

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
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820141003_CumulativeRefundAndLoyaltyEarnUniqueness'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_loyalty_transactions_order_earn] ON [loyalty_transactions] ([order_id]) WHERE [order_id] IS NOT NULL AND [type] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820141003_CumulativeRefundAndLoyaltyEarnUniqueness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260820141003_CumulativeRefundAndLoyaltyEarnUniqueness', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820234946_ItemTransactionIdForRefund'
)
BEGIN
    ALTER TABLE [payments] ADD [item_transaction_id] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820234946_ItemTransactionIdForRefund'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260820234946_ItemTransactionIdForRefund', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821185942_CouponUsageUniquenessSprint8'
)
BEGIN

    IF EXISTS (
        SELECT 1 FROM coupon_usages
        GROUP BY coupon_id, order_id
        HAVING COUNT(*) > 1
    )
    BEGIN
        RAISERROR (N'coupon_usages tablosunda ayni (coupon_id, order_id) icin BIRDEN FAZLA satir var. UX_coupon_usages_coupon_order kurulamaz. Fazla satirlar ELLE incelenmeli - bu migration satir SILMEZ (silmek coupons.used_count ile defteri ayirirdi).', 16, 1);
    END

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821185942_CouponUsageUniquenessSprint8'
)
BEGIN
    CREATE UNIQUE INDEX [UX_coupon_usages_coupon_order] ON [coupon_usages] ([coupon_id], [order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821185942_CouponUsageUniquenessSprint8'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821185942_CouponUsageUniquenessSprint8', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    ALTER TABLE [stock_notification_requests] ADD [unsubscribe_token] nvarchar(64) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    ALTER TABLE [price_drop_subscriptions] ADD [unsubscribe_token] nvarchar(64) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    UPDATE stock_notification_requests SET unsubscribe_token = CONVERT(NVARCHAR(64), NEWID()) WHERE unsubscribe_token = N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    UPDATE price_drop_subscriptions SET unsubscribe_token = CONVERT(NVARCHAR(64), NEWID()) WHERE unsubscribe_token = N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    CREATE UNIQUE INDEX [UX_stock_notification_requests_token] ON [stock_notification_requests] ([unsubscribe_token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    CREATE UNIQUE INDEX [UX_price_drop_subscriptions_token] ON [price_drop_subscriptions] ([unsubscribe_token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821192219_UnsubscribeTokensSprint8'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821192219_UnsubscribeTokensSprint8', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821202442_RefereeRewardUniquenessSprint8'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_store_credit_referee_reward] ON [store_credit_transactions] ([customer_id]) WHERE [reason] = N''Referans ödülü (davet edilen)''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821202442_RefereeRewardUniquenessSprint8'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821202442_RefereeRewardUniquenessSprint8', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    DELETE FROM customers
    WHERE email COLLATE Latin1_General_BIN2 IN (
            N'iris.kalite@example.com' COLLATE Latin1_General_BIN2,
            N'' + NCHAR(305) + N'ris.kalite@example.com' COLLATE Latin1_General_BIN2)
      AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = customers.id);

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    DECLARE @hasarli INT = (
        SELECT COUNT(*) FROM customers
        WHERE email COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(305) + N'%' COLLATE Latin1_General_BIN2
           OR email COLLATE Latin1_General_BIN2 LIKE N'%' + NCHAR(304) + N'%' COLLATE Latin1_General_BIN2);
    IF @hasarli > 0
        RAISERROR(N'B1 UYARI: %d musteri e-postasi Turkce kucultme ile HASARLI (ici ''i'' yerine ''i'' ya da ''I'' iceriyor). OTOMATIK ONARILMADI - karakter degisikligi TAHMIN olur. Bu satirlari elle inceleyin: SELECT id, email FROM customers WHERE email COLLATE Latin1_General_BIN2 LIKE N''%%'' + NCHAR(305) + N''%%'' COLLATE Latin1_General_BIN2 OR email COLLATE Latin1_General_BIN2 LIKE N''%%'' + NCHAR(304) + N''%%'' COLLATE Latin1_General_BIN2;', 16, 1, @hasarli);

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    IF EXISTS (
        SELECT 1 FROM customers
        GROUP BY LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2
        HAVING COUNT(*) > 1)
        RAISERROR(N'B1 DURDURULDU: e-postalari kanonik bicime normalize etmek IKI VEYA DAHA FAZLA satiri ayni degere getirecek (IX_customers_email UNIQUE). Hicbir satir DEGISTIRILMEDI. Cakisan gruplari inceleyip mukerrer hesaplari birlestirin: SELECT LOWER(email COLLATE Latin1_General_CI_AS) AS kanonik, COUNT(*) FROM customers GROUP BY LOWER(email COLLATE Latin1_General_CI_AS) HAVING COUNT(*) > 1;', 16, 1);

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    UPDATE customers
    SET email = LOWER(email COLLATE Latin1_General_CI_AS)
    WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'sellers')
        UPDATE sellers
        SET email = LOWER(email COLLATE Latin1_General_CI_AS)
        WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    UPDATE coupons
    SET code = UPPER(code COLLATE Latin1_General_CI_AS)
    WHERE code COLLATE Latin1_General_BIN2 <> UPPER(code COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN

    UPDATE stock_notification_requests
    SET email = LOWER(email COLLATE Latin1_General_CI_AS)
    WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;

    UPDATE price_drop_subscriptions
    SET email = LOWER(email COLLATE Latin1_General_CI_AS)
    WHERE email COLLATE Latin1_General_BIN2 <> LOWER(email COLLATE Latin1_General_CI_AS) COLLATE Latin1_General_BIN2;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822015532_EmailKanonikNormalizasyon'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822015532_EmailKanonikNormalizasyon', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822134317_StokHareketiIsaretliDuzeltme'
)
BEGIN

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

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822134317_StokHareketiIsaretliDuzeltme'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822134317_StokHareketiIsaretliDuzeltme', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824104731_YetimStokReferansButunlugu'
)
BEGIN

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

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824104731_YetimStokReferansButunlugu'
)
BEGIN

    DELETE ps
    FROM product_stocks ps
    WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.id = ps.product_id)
      AND ps.reserved_quantity = 0
      AND NOT EXISTS (SELECT 1 FROM stock_reservations r WHERE r.product_id = ps.product_id)
      AND NOT EXISTS (SELECT 1 FROM stock_movements  m WHERE m.product_id = ps.product_id)
      AND NOT EXISTS (SELECT 1 FROM order_items      i WHERE i.product_id = ps.product_id);

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824104731_YetimStokReferansButunlugu'
)
BEGIN

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

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824104731_YetimStokReferansButunlugu'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824104731_YetimStokReferansButunlugu', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN

    DECLARE @ihlal TABLE (iliski NVARCHAR(200), adet INT);

    INSERT INTO @ihlal SELECT N'cart_items.product_id', COUNT(*) FROM [cart_items] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'collection_items.collection_id', COUNT(*) FROM [collection_items] c WHERE c.[collection_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [collections] p WHERE p.[id] = c.[collection_id]);
    INSERT INTO @ihlal SELECT N'collection_items.product_id', COUNT(*) FROM [collection_items] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'coupon_usages.coupon_id', COUNT(*) FROM [coupon_usages] c WHERE c.[coupon_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [coupons] p WHERE p.[id] = c.[coupon_id]);
    INSERT INTO @ihlal SELECT N'coupon_usages.customer_id', COUNT(*) FROM [coupon_usages] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'coupon_usages.order_id', COUNT(*) FROM [coupon_usages] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'customer_devices.customer_id', COUNT(*) FROM [customer_devices] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'invoices.customer_id', COUNT(*) FROM [invoices] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'invoices.order_id', COUNT(*) FROM [invoices] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'loyalty_transactions.customer_id', COUNT(*) FROM [loyalty_transactions] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'loyalty_transactions.order_id', COUNT(*) FROM [loyalty_transactions] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'order_snapshot_items.order_snapshot_id', COUNT(*) FROM [order_snapshot_items] c WHERE c.[order_snapshot_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [order_snapshots] p WHERE p.[id] = c.[order_snapshot_id]);
    INSERT INTO @ihlal SELECT N'order_snapshot_items.product_id', COUNT(*) FROM [order_snapshot_items] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'order_snapshots.customer_id', COUNT(*) FROM [order_snapshots] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'order_snapshots.order_id', COUNT(*) FROM [order_snapshots] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'order_status_histories.order_id', COUNT(*) FROM [order_status_histories] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'orders.address_id', COUNT(*) FROM [orders] c WHERE c.[address_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [addresses] p WHERE p.[id] = c.[address_id]);
    INSERT INTO @ihlal SELECT N'payments.order_id', COUNT(*) FROM [payments] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'price_drop_subscriptions.product_id', COUNT(*) FROM [price_drop_subscriptions] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'product_attributes.product_id', COUNT(*) FROM [product_attributes] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'product_images.product_id', COUNT(*) FROM [product_images] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'product_questions.customer_id', COUNT(*) FROM [product_questions] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'product_questions.product_id', COUNT(*) FROM [product_questions] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'product_reviews.customer_id', COUNT(*) FROM [product_reviews] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'products.category_id', COUNT(*) FROM [products] c WHERE c.[category_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [categories] p WHERE p.[id] = c.[category_id]);
    INSERT INTO @ihlal SELECT N'products.sub_category_id', COUNT(*) FROM [products] c WHERE c.[sub_category_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [sub_categories] p WHERE p.[id] = c.[sub_category_id]);
    INSERT INTO @ihlal SELECT N'recently_viewed_products.customer_id', COUNT(*) FROM [recently_viewed_products] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'recently_viewed_products.product_id', COUNT(*) FROM [recently_viewed_products] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'return_requests.customer_id', COUNT(*) FROM [return_requests] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'return_requests.order_id', COUNT(*) FROM [return_requests] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'return_requests.product_id', COUNT(*) FROM [return_requests] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'review_helpful_votes.customer_id', COUNT(*) FROM [review_helpful_votes] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'security_events.customer_id', COUNT(*) FROM [security_events] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'shipments.order_id', COUNT(*) FROM [shipments] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'size_guide_entries.category_id', COUNT(*) FROM [size_guide_entries] c WHERE c.[category_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [categories] p WHERE p.[id] = c.[category_id]);
    INSERT INTO @ihlal SELECT N'stock_movements.product_id', COUNT(*) FROM [stock_movements] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'stock_notification_requests.product_id', COUNT(*) FROM [stock_notification_requests] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'stock_reservations.order_id', COUNT(*) FROM [stock_reservations] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'stock_reservations.product_id', COUNT(*) FROM [stock_reservations] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);
    INSERT INTO @ihlal SELECT N'store_credit_transactions.customer_id', COUNT(*) FROM [store_credit_transactions] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'store_credit_transactions.order_id', COUNT(*) FROM [store_credit_transactions] c WHERE c.[order_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [orders] p WHERE p.[id] = c.[order_id]);
    INSERT INTO @ihlal SELECT N'sub_categories.category_id', COUNT(*) FROM [sub_categories] c WHERE c.[category_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [categories] p WHERE p.[id] = c.[category_id]);
    INSERT INTO @ihlal SELECT N'user_sessions.customer_id', COUNT(*) FROM [user_sessions] c WHERE c.[customer_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [customers] p WHERE p.[id] = c.[customer_id]);
    INSERT INTO @ihlal SELECT N'wishlist_items.product_id', COUNT(*) FROM [wishlist_items] c WHERE c.[product_id] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [products] p WHERE p.[id] = c.[product_id]);

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

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [addresses] DROP CONSTRAINT [FK_addresses_customers_customer_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [cart_items] DROP CONSTRAINT [FK_cart_items_carts_cart_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [carts] DROP CONSTRAINT [FK_carts_customers_customer_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_items] DROP CONSTRAINT [FK_order_items_orders_order_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_items] DROP CONSTRAINT [FK_order_items_products_product_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [orders] DROP CONSTRAINT [FK_orders_customers_customer_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_reviews] DROP CONSTRAINT [FK_product_reviews_products_product_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [wishlist_items] DROP CONSTRAINT [FK_wishlist_items_customers_customer_id];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_wishlist_items_product_id] ON [wishlist_items] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_store_credit_transactions_order_id] ON [store_credit_transactions] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_stock_reservations_product_id] ON [stock_reservations] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_review_helpful_votes_customer_id] ON [review_helpful_votes] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_return_requests_product_id] ON [return_requests] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_recently_viewed_products_product_id] ON [recently_viewed_products] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_products_sub_category_id] ON [products] ([sub_category_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_product_questions_customer_id] ON [product_questions] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_orders_address_id] ON [orders] ([address_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_order_snapshots_customer_id] ON [order_snapshots] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_order_snapshots_order_id] ON [order_snapshots] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_order_snapshot_items_order_snapshot_id] ON [order_snapshot_items] ([order_snapshot_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_order_snapshot_items_product_id] ON [order_snapshot_items] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_coupon_usages_customer_id] ON [coupon_usages] ([customer_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_coupon_usages_order_id] ON [coupon_usages] ([order_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_collection_items_product_id] ON [collection_items] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    CREATE INDEX [IX_cart_items_product_id] ON [cart_items] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [addresses] ADD CONSTRAINT [FK_addresses_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [cart_items] ADD CONSTRAINT [FK_cart_items_cart_id] FOREIGN KEY ([cart_id]) REFERENCES [carts] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [cart_items] ADD CONSTRAINT [FK_cart_items_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [carts] ADD CONSTRAINT [FK_carts_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [collection_items] ADD CONSTRAINT [FK_collection_items_collection_id] FOREIGN KEY ([collection_id]) REFERENCES [collections] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [collection_items] ADD CONSTRAINT [FK_collection_items_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [coupon_usages] ADD CONSTRAINT [FK_coupon_usages_coupon_id] FOREIGN KEY ([coupon_id]) REFERENCES [coupons] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [coupon_usages] ADD CONSTRAINT [FK_coupon_usages_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [coupon_usages] ADD CONSTRAINT [FK_coupon_usages_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [customer_devices] ADD CONSTRAINT [FK_customer_devices_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [invoices] ADD CONSTRAINT [FK_invoices_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [invoices] ADD CONSTRAINT [FK_invoices_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [loyalty_transactions] ADD CONSTRAINT [FK_loyalty_transactions_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [loyalty_transactions] ADD CONSTRAINT [FK_loyalty_transactions_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_items] ADD CONSTRAINT [FK_order_items_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_items] ADD CONSTRAINT [FK_order_items_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_snapshot_items] ADD CONSTRAINT [FK_order_snapshot_items_order_snapshot_id] FOREIGN KEY ([order_snapshot_id]) REFERENCES [order_snapshots] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_snapshot_items] ADD CONSTRAINT [FK_order_snapshot_items_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_snapshots] ADD CONSTRAINT [FK_order_snapshots_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_snapshots] ADD CONSTRAINT [FK_order_snapshots_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [order_status_histories] ADD CONSTRAINT [FK_order_status_histories_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [orders] ADD CONSTRAINT [FK_orders_address_id] FOREIGN KEY ([address_id]) REFERENCES [addresses] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [orders] ADD CONSTRAINT [FK_orders_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [payments] ADD CONSTRAINT [FK_payments_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [price_drop_subscriptions] ADD CONSTRAINT [FK_price_drop_subscriptions_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_attributes] ADD CONSTRAINT [FK_product_attributes_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_images] ADD CONSTRAINT [FK_product_images_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_questions] ADD CONSTRAINT [FK_product_questions_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_questions] ADD CONSTRAINT [FK_product_questions_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_reviews] ADD CONSTRAINT [FK_product_reviews_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [product_reviews] ADD CONSTRAINT [FK_product_reviews_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [products] ADD CONSTRAINT [FK_products_category_id] FOREIGN KEY ([category_id]) REFERENCES [categories] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [products] ADD CONSTRAINT [FK_products_sub_category_id] FOREIGN KEY ([sub_category_id]) REFERENCES [sub_categories] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [recently_viewed_products] ADD CONSTRAINT [FK_recently_viewed_products_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [recently_viewed_products] ADD CONSTRAINT [FK_recently_viewed_products_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [return_requests] ADD CONSTRAINT [FK_return_requests_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [return_requests] ADD CONSTRAINT [FK_return_requests_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [return_requests] ADD CONSTRAINT [FK_return_requests_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [review_helpful_votes] ADD CONSTRAINT [FK_review_helpful_votes_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [security_events] ADD CONSTRAINT [FK_security_events_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [shipments] ADD CONSTRAINT [FK_shipments_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [size_guide_entries] ADD CONSTRAINT [FK_size_guide_entries_category_id] FOREIGN KEY ([category_id]) REFERENCES [categories] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [stock_movements] ADD CONSTRAINT [FK_stock_movements_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [stock_notification_requests] ADD CONSTRAINT [FK_stock_notification_requests_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [stock_reservations] ADD CONSTRAINT [FK_stock_reservations_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [stock_reservations] ADD CONSTRAINT [FK_stock_reservations_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [store_credit_transactions] ADD CONSTRAINT [FK_store_credit_transactions_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [store_credit_transactions] ADD CONSTRAINT [FK_store_credit_transactions_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [sub_categories] ADD CONSTRAINT [FK_sub_categories_category_id] FOREIGN KEY ([category_id]) REFERENCES [categories] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [user_sessions] ADD CONSTRAINT [FK_user_sessions_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [wishlist_items] ADD CONSTRAINT [FK_wishlist_items_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    ALTER TABLE [wishlist_items] ADD CONSTRAINT [FK_wishlist_items_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824124039_ReferansButunluguTekMerkez'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824124039_ReferansButunluguTekMerkez', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824134356_UcEksikReferans'
)
BEGIN

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

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824134356_UcEksikReferans'
)
BEGIN
    CREATE INDEX [IX_invoice_items_product_id] ON [invoice_items] ([product_id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824134356_UcEksikReferans'
)
BEGIN
    ALTER TABLE [invoice_items] ADD CONSTRAINT [FK_invoice_items_invoice_id] FOREIGN KEY ([invoice_id]) REFERENCES [invoices] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824134356_UcEksikReferans'
)
BEGIN
    ALTER TABLE [invoice_items] ADD CONSTRAINT [FK_invoice_items_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824134356_UcEksikReferans'
)
BEGIN
    ALTER TABLE [review_helpful_votes] ADD CONSTRAINT [FK_review_helpful_votes_review_id] FOREIGN KEY ([review_id]) REFERENCES [product_reviews] ([id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824134356_UcEksikReferans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824134356_UcEksikReferans', N'8.0.30');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829010821_KargoKalemiIcinProductIdNullable'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[invoice_items]') AND [c].[name] = N'product_id');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [invoice_items] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [invoice_items] ALTER COLUMN [product_id] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829010821_KargoKalemiIcinProductIdNullable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829010821_KargoKalemiIcinProductIdNullable', N'8.0.30');
END;
GO

COMMIT;
GO

