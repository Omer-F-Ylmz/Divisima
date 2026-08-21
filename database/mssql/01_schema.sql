-- Divisima e-ticaret veritabani (MSSQL) - 44 tablo
-- Entity siniflarindan otomatik uretildi. Kolon adlari entity ile birebir (snake_case).

-- CREATE DATABASE Divisima;
-- GO
-- USE Divisima;
-- GO

CREATE TABLE addresses (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    title NVARCHAR(256) NOT NULL,
    full_name NVARCHAR(256) NOT NULL,
    phone NVARCHAR(256) NULL,   -- KVKK anonimlestirmesinde NULL yazilir
    city NVARCHAR(256) NOT NULL,
    district NVARCHAR(256) NOT NULL,
    full_address NVARCHAR(256) NOT NULL,
    zip_code NVARCHAR(256) NULL,
    is_default BIT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE audit_logs (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    table_name NVARCHAR(256) NOT NULL,
    entity_id NVARCHAR(256) NOT NULL,
    action NVARCHAR(256) NOT NULL,
    changes NVARCHAR(256) NULL,
    user_id NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE carts (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL,
    reminder_sent_at DATETIME2 NULL
);

CREATE TABLE cart_items (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    cart_id INT NOT NULL,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    quantity INT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE categories (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    name NVARCHAR(256) NOT NULL,
    slug NVARCHAR(256) NOT NULL,
    display_order INT NOT NULL,
    vat_rate DECIMAL(5,4) NULL,   -- kalem bazli KDV (NULL = EInvoice:KdvRate varsayilanina duser)
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE collections (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    name NVARCHAR(256) NOT NULL,
    slug NVARCHAR(256) NOT NULL,
    collection_type TINYINT NOT NULL,
    curator_name NVARCHAR(256) NULL,
    subtitle NVARCHAR(256) NULL,
    gradient NVARCHAR(256) NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE collection_items (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    collection_id INT NOT NULL,
    product_id INT NOT NULL,
    display_order INT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE consent_records (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NULL,
    consent_type NVARCHAR(256) NOT NULL,
    document_version NVARCHAR(256) NOT NULL,
    granted BIT NOT NULL,
    ip_address NVARCHAR(256) NULL,
    user_agent NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE contents (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    slug NVARCHAR(256) NOT NULL,
    title_tr NVARCHAR(256) NOT NULL,
    title_en NVARCHAR(256) NULL,
    body_tr NVARCHAR(MAX) NOT NULL,
    body_en NVARCHAR(MAX) NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE coupons (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    code NVARCHAR(256) NOT NULL,
    discount_type TINYINT NOT NULL,
    value DECIMAL(18,2) NOT NULL,
    min_amount DECIMAL(18,2) NOT NULL,
    max_discount_amount DECIMAL(18,2) NULL,
    expire_date DATETIME2 NULL,
    usage_limit INT NOT NULL,
    per_user_limit INT NOT NULL DEFAULT 0,
    used_count INT NOT NULL,
    first_order_only BIT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL,
    row_version ROWVERSION
);

CREATE TABLE coupon_usages (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    coupon_id INT NOT NULL,
    customer_id INT NOT NULL,
    order_id INT NOT NULL,
    discount_applied DECIMAL(18,2) NOT NULL,
    created_at DATETIME2 NOT NULL
);
-- Siparis basina TEK kupon kullanimi (Sprint 8 madde 1). coupons.used_count artik bu
-- tablodan TURETILIYOR; sayacin dogrulugu "ayni siparis iki kez sayilamaz" garantisine bagli.
-- At-least-once bir yeniden deneme (outbox) ya da paralel bir callback ikinci satiri
-- yazamaz. UX_loyalty_transactions_order_earn ile ayni kalip.
CREATE UNIQUE INDEX UX_coupon_usages_coupon_order ON coupon_usages (coupon_id, order_id);

CREATE TABLE customers (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    name NVARCHAR(256) NOT NULL,
    email NVARCHAR(256) NOT NULL,
    user_type TINYINT NOT NULL DEFAULT 2,
    phone NVARCHAR(256) NULL,   -- KVKK anonimlestirmesinde NULL yazilir
    address NVARCHAR(256) NULL,
    city NVARCHAR(256) NULL,
    gender NVARCHAR(256) NULL,
    password_salt VARBINARY(MAX) NOT NULL,
    password_hash VARBINARY(MAX) NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL,
    last_login_at DATETIME2 NULL,
    email_verified BIT NOT NULL,
    email_verification_token NVARCHAR(MAX) NULL,
    email_verification_sent_at DATETIME2 NULL,
    password_reset_token NVARCHAR(MAX) NULL,
    password_reset_expiry DATETIME2 NULL,
    two_factor_enabled BIT NOT NULL,
    two_factor_secret NVARCHAR(256) NULL,
    two_factor_code NVARCHAR(256) NULL,
    two_factor_code_expiry DATETIME2 NULL,
    failed_login_attempts INT NOT NULL,
    lockout_end DATETIME2 NULL,
    birthdate DATETIME2 NULL,
    notify_email BIT NOT NULL DEFAULT 1,
    notify_sms BIT NOT NULL DEFAULT 1,
    notify_push BIT NOT NULL DEFAULT 1,
    loyalty_points INT NOT NULL,
    store_credit DECIMAL(18,2) NOT NULL,
    referral_code NVARCHAR(256) NULL,
    referred_by INT NULL,
    last_order_at DATETIME2 NULL,
    last_winback_sent_at DATETIME2 NULL,
    birthday_offer_sent_year DATETIME2 NULL
);

CREATE TABLE customer_devices (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    device_token NVARCHAR(MAX) NOT NULL,
    platform TINYINT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    last_used_at DATETIME2 NULL
);

CREATE TABLE gift_cards (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    code NVARCHAR(256) NOT NULL,
    initial_amount DECIMAL(18,2) NOT NULL,
    balance DECIMAL(18,2) NOT NULL,
    is_active BIT NOT NULL,
    redeemed_by INT NULL,
    created_at DATETIME2 NOT NULL,
    redeemed_at DATETIME2 NULL
);

CREATE TABLE invoices (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    customer_id INT NOT NULL,
    invoice_number NVARCHAR(256) NOT NULL,
    invoice_type TINYINT NOT NULL,
    tax_number NVARCHAR(256) NULL,
    company_name NVARCHAR(256) NULL,
    subtotal DECIMAL(18,2) NOT NULL,
    tax_rate DECIMAL(5,4) NOT NULL,   -- kalemlerin AGIRLIKLI ORTALAMASI (18,2 olsaydi 0.1467 -> 0.15 yuvarlanirdi)
    tax_amount DECIMAL(18,2) NOT NULL,
    total DECIMAL(18,2) NOT NULL,
    status TINYINT NOT NULL,
    provider_invoice_id NVARCHAR(256) NULL,
    pdf_url NVARCHAR(MAX) NULL,
    created_at DATETIME2 NOT NULL
);

-- Fatura kalemleri: KALEM BAZLI KDV. Oran fatura kesildigi anda DONDURULUR (snapshot) -
-- kategori/urun orani sonradan degisse bile kesilmis fatura degismez.
-- invoices.tax_rate artik kalemlerin AGIRLIKLI ORTALAMASIDIR.
CREATE TABLE invoice_items (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    invoice_id INT NOT NULL,
    product_id INT NOT NULL,
    product_name NVARCHAR(200) NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(18,2) NOT NULL,
    line_subtotal DECIMAL(18,2) NOT NULL,
    vat_rate DECIMAL(5,4) NOT NULL,
    vat_amount DECIMAL(18,2) NOT NULL,
    line_total DECIMAL(18,2) NOT NULL,
    created_at DATETIME2 NOT NULL
);
CREATE INDEX IX_invoice_items_invoice_id ON invoice_items (invoice_id);

CREATE TABLE loyalty_transactions (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    points INT NOT NULL,
    type TINYINT NOT NULL,
    reason NVARCHAR(256) NOT NULL,
    order_id INT NULL,
    created_at DATETIME2 NOT NULL
);
-- Siparis basina TEK kazanim (filtreli UNIQUE): eszamanli odeme callback'leri ayni siparise
-- ikinci bir Earn satiri yazamaz. Redeem (geri alim) ayni order_id ile serbesttir.
CREATE UNIQUE INDEX UX_loyalty_transactions_order_earn ON loyalty_transactions (order_id)
    WHERE order_id IS NOT NULL AND type = 0;

CREATE TABLE orders (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    order_number NVARCHAR(256) NOT NULL,
    request_id NVARCHAR(256) NULL,
    status TINYINT NOT NULL,
    subtotal DECIMAL(18,2) NOT NULL,
    discount_amount DECIMAL(18,2) NOT NULL,
    shipping_cost DECIMAL(18,2) NOT NULL,
    total_price DECIMAL(18,2) NOT NULL,
    currency NVARCHAR(256) NOT NULL DEFAULT N'TRY',
    coupon_code NVARCHAR(256) NULL,
    address_id INT NULL,
    payment_type TINYINT NOT NULL,
    store_credit_used DECIMAL(18,2) NOT NULL DEFAULT 0,
    -- Kumulatif iade sayaci: bu siparis icin bugune kadar iade edilen TOPLAM tutar.
    -- RefundToSourceAsync atomik artirir; toplam total_price'i asamaz.
    refunded_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    installment_count TINYINT NOT NULL DEFAULT 1,
    is_online_payment_done BIT NOT NULL,
    payment_id NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL,
    delivered_at DATETIME2 NULL,
    review_invite_sent_at DATETIME2 NULL
);

CREATE TABLE order_items (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(18,2) NOT NULL,
    is_cancelled BIT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE order_snapshots (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    customer_id INT NOT NULL,
    customer_full_name NVARCHAR(256) NOT NULL,
    shipping_address NVARCHAR(256) NULL,
    status TINYINT NOT NULL,
    subtotal DECIMAL(18,2) NOT NULL,
    discount_amount DECIMAL(18,2) NOT NULL,
    shipping_cost DECIMAL(18,2) NOT NULL,
    total_price DECIMAL(18,2) NOT NULL,
    coupon_code NVARCHAR(256) NULL,
    snapshot_created_at DATETIME2 NOT NULL,
    order_created_at DATETIME2 NOT NULL
);

CREATE TABLE order_snapshot_items (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_snapshot_id INT NOT NULL,
    product_id INT NOT NULL,
    product_name NVARCHAR(256) NOT NULL,
    brand NVARCHAR(256) NOT NULL,
    product_price DECIMAL(18,2) NOT NULL,
    size NVARCHAR(256) NOT NULL,
    quantity INT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE order_status_histories (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    status TINYINT NOT NULL,
    note NVARCHAR(256) NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE outbox_messages (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    event_type NVARCHAR(256) NOT NULL,
    payload NVARCHAR(MAX) NOT NULL,
    status TINYINT NOT NULL,
    retry_count INT NOT NULL,
    error NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL,
    processed_at DATETIME2 NULL
);

CREATE TABLE payments (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    payment_provider NVARCHAR(256) NOT NULL,
    payment_status TINYINT NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    paid_price DECIMAL(18,2) NULL,
    installment_count TINYINT NOT NULL DEFAULT 1,
    installment_fee DECIMAL(18,2) NULL,
    currency NVARCHAR(256) NULL,
    fraud_status NVARCHAR(256) NULL,
    transaction_id NVARCHAR(256) NULL,
    -- E2b: IADE bu kimligi ister (paymentId DEGIL). Olculdu: ayni odemede paymentId=37399936
    -- iken itemTransaction paymentTransactionId=39316344; yanlisiyla cagrilinca Iyzico
    -- "Bu isyerine ait odeme kirilim kaydi bulunamadi" ile reddediyor.
    item_transaction_id NVARCHAR(120) NULL,
    conversation_id NVARCHAR(256) NULL,
    token NVARCHAR(MAX) NULL,
    paid_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE price_drop_subscriptions (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    email NVARCHAR(256) NOT NULL,
    subscribed_price DECIMAL(18,2) NOT NULL,
    is_notified BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    notified_at DATETIME2 NULL,
    -- Sprint 8 madde 10: abonelikten cikma jetonu. Abonelik ANONIM kurulabildigi icin cikma
    -- yolu kimlik dogrulamasi isteyemez; jeton e-postadaki baglantinin sahiplik kanitidir.
    unsubscribe_token NVARCHAR(64) NOT NULL
);
CREATE UNIQUE INDEX UX_price_drop_subscriptions_token ON price_drop_subscriptions (unsubscribe_token);

CREATE TABLE products (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    name NVARCHAR(256) NOT NULL,
    brand NVARCHAR(256) NOT NULL,
    category_id INT NOT NULL,
    sub_category_id INT NULL,
    price DECIMAL(18,2) NOT NULL,
    sale_price DECIMAL(18,2) NULL,
    sale_start DATETIME2 NULL,
    sale_end DATETIME2 NULL,
    old_price DECIMAL(18,2) NULL,
    description NVARCHAR(MAX) NOT NULL,
    color_hex NVARCHAR(256) NOT NULL,
    variant_group_id NVARCHAR(256) NULL,
    image_url NVARCHAR(MAX) NULL,
    product_type TINYINT NOT NULL,
    vat_rate DECIMAL(5,4) NULL,   -- kategori oranini EZEN urun bazli KDV (NULL = kategoriden alinir)
    average_rating DECIMAL(18,2) NOT NULL DEFAULT 0,
    review_count INT NOT NULL DEFAULT 0,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE product_attributes (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    attribute_key NVARCHAR(256) NOT NULL,
    attribute_value NVARCHAR(256) NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE product_images (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    image_url NVARCHAR(MAX) NOT NULL,
    sort_order INT NOT NULL,
    is_primary BIT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE product_questions (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    customer_id INT NOT NULL,
    question NVARCHAR(MAX) NOT NULL,
    answer NVARCHAR(MAX) NULL,
    answered_by INT NULL,
    is_answered BIT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    answered_at DATETIME2 NULL
);

CREATE TABLE product_reviews (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    customer_id INT NOT NULL,
    rating INT NOT NULL,
    comment NVARCHAR(MAX) NOT NULL,
    is_verified_purchase BIT NOT NULL,
    helpful_count INT NOT NULL,
    review_status TINYINT NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE product_stocks (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    stock_quantity INT NOT NULL,
    reserved_quantity INT NOT NULL,
    row_version ROWVERSION,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE recently_viewed_products (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    product_id INT NOT NULL,
    viewed_at DATETIME2 NOT NULL
);

CREATE TABLE return_requests (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    customer_id INT NOT NULL,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    quantity INT NOT NULL,
    reason TINYINT NOT NULL,
    description NVARCHAR(MAX) NULL,
    return_type TINYINT NOT NULL,
    status TINYINT NOT NULL,
    refund_amount DECIMAL(18,2) NOT NULL,
    refund_id NVARCHAR(256) NULL,
    admin_note NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL,
    processed_at DATETIME2 NULL
);

CREATE TABLE review_helpful_votes (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    review_id INT NOT NULL,
    customer_id INT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE security_events (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    event_type NVARCHAR(256) NOT NULL,
    severity NVARCHAR(256) NOT NULL,
    customer_id INT NULL,
    ip_address NVARCHAR(256) NULL,
    user_agent NVARCHAR(256) NULL,
    detail NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE shipments (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    carrier TINYINT NOT NULL,
    tracking_number NVARCHAR(256) NOT NULL,
    status TINYINT NOT NULL,
    last_status_text NVARCHAR(256) NULL,
    shipped_at DATETIME2 NULL,
    estimated_delivery DATETIME2 NULL,
    delivered_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL,
    last_checked_at DATETIME2 NULL
);

CREATE TABLE size_guide_entries (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    category_id INT NOT NULL,
    size_label NVARCHAR(256) NOT NULL,
    bust_cm DECIMAL(18,2) NULL,
    waist_cm DECIMAL(18,2) NULL,
    hip_cm DECIMAL(18,2) NULL,
    length_cm DECIMAL(18,2) NULL,
    is_active BIT NOT NULL,
    sort_order INT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE stock_movements (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    movement_type TINYINT NOT NULL,
    quantity INT NOT NULL,
    reference_id INT NULL,
    note NVARCHAR(256) NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE stock_notification_requests (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    email NVARCHAR(256) NOT NULL,
    is_notified BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    notified_at DATETIME2 NULL,
    -- Sprint 8 madde 10: abonelikten cikma jetonu (price_drop_subscriptions ile ayni gerekce).
    unsubscribe_token NVARCHAR(64) NOT NULL
);
CREATE UNIQUE INDEX UX_stock_notification_requests_token ON stock_notification_requests (unsubscribe_token);

CREATE TABLE stock_reservations (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    order_id INT NOT NULL,
    product_id INT NOT NULL,
    size NVARCHAR(256) NOT NULL,
    quantity INT NOT NULL,
    status TINYINT NOT NULL,
    expires_at DATETIME2 NOT NULL,
    created_at DATETIME2 NOT NULL,
    closed_at DATETIME2 NULL
);

CREATE TABLE store_credit_transactions (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    type TINYINT NOT NULL,
    reason NVARCHAR(256) NOT NULL,
    order_id INT NULL,
    created_at DATETIME2 NOT NULL
);
-- Sprint 8 madde 3: DAVET EDILEN referans odulu MUSTERI BASINA TEKIL. Yan etkiler outbox'a
-- tasindi ve at-least-once oldu; oncesinde tek koruma uygulama katmanindaki oku-sonra-davran
-- guard'iydi ve eszamanli iki teslimat IKI KEZ odeyebilirdi. FILTRELI: "davet EDEN" odulu
-- TEKRARLANABILIR (bir kullanici birden fazla kisiyi davet edebilir).
-- DIKKAT: filtre ReferralManager.RefereeRewardReason metnine BIREBIR baglidir - ikisi BIRLIKTE
-- degistirilmeli, aksi halde koruma SESSIZCE kalkar.
CREATE UNIQUE INDEX UX_store_credit_referee_reward ON store_credit_transactions (customer_id)
    WHERE reason = N'Referans ödülü (davet edilen)';

CREATE TABLE sub_categories (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    category_id INT NOT NULL,
    name NVARCHAR(256) NOT NULL,
    slug NVARCHAR(256) NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL,
    updated_at DATETIME2 NULL
);

CREATE TABLE user_sessions (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    refresh_token NVARCHAR(MAX) NOT NULL,
    device NVARCHAR(256) NULL,
    ip_address NVARCHAR(256) NULL,
    expires_at DATETIME2 NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2 NOT NULL
);

CREATE TABLE wishlist_items (
    id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    customer_id INT NOT NULL,
    product_id INT NOT NULL,
    created_at DATETIME2 NOT NULL
);

-- === Foreign Key kisitlari (yetim kayit onleme) ===
ALTER TABLE addresses ADD CONSTRAINT FK_addresses_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE carts ADD CONSTRAINT FK_carts_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE cart_items ADD CONSTRAINT FK_cart_items_cart_id FOREIGN KEY (cart_id) REFERENCES carts(id);
ALTER TABLE cart_items ADD CONSTRAINT FK_cart_items_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE collection_items ADD CONSTRAINT FK_collection_items_collection_id FOREIGN KEY (collection_id) REFERENCES collections(id);
ALTER TABLE collection_items ADD CONSTRAINT FK_collection_items_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE consent_records ADD CONSTRAINT FK_consent_records_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE coupon_usages ADD CONSTRAINT FK_coupon_usages_coupon_id FOREIGN KEY (coupon_id) REFERENCES coupons(id);
ALTER TABLE coupon_usages ADD CONSTRAINT FK_coupon_usages_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE coupon_usages ADD CONSTRAINT FK_coupon_usages_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE customer_devices ADD CONSTRAINT FK_customer_devices_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE invoices ADD CONSTRAINT FK_invoices_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE invoices ADD CONSTRAINT FK_invoices_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE loyalty_transactions ADD CONSTRAINT FK_loyalty_transactions_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE loyalty_transactions ADD CONSTRAINT FK_loyalty_transactions_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE orders ADD CONSTRAINT FK_orders_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE orders ADD CONSTRAINT FK_orders_address_id FOREIGN KEY (address_id) REFERENCES addresses(id);
ALTER TABLE orders ADD CONSTRAINT FK_orders_payment_id FOREIGN KEY (payment_id) REFERENCES payments(id);
ALTER TABLE order_items ADD CONSTRAINT FK_order_items_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE order_items ADD CONSTRAINT FK_order_items_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE order_snapshots ADD CONSTRAINT FK_order_snapshots_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE order_snapshots ADD CONSTRAINT FK_order_snapshots_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE order_snapshot_items ADD CONSTRAINT FK_order_snapshot_items_order_snapshot_id FOREIGN KEY (order_snapshot_id) REFERENCES order_snapshots(id);
ALTER TABLE order_snapshot_items ADD CONSTRAINT FK_order_snapshot_items_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE order_status_histories ADD CONSTRAINT FK_order_status_histories_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE payments ADD CONSTRAINT FK_payments_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE price_drop_subscriptions ADD CONSTRAINT FK_price_drop_subscriptions_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE products ADD CONSTRAINT FK_products_category_id FOREIGN KEY (category_id) REFERENCES categories(id);
ALTER TABLE products ADD CONSTRAINT FK_products_sub_category_id FOREIGN KEY (sub_category_id) REFERENCES sub_categories(id);
ALTER TABLE product_attributes ADD CONSTRAINT FK_product_attributes_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE product_images ADD CONSTRAINT FK_product_images_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE product_questions ADD CONSTRAINT FK_product_questions_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE product_questions ADD CONSTRAINT FK_product_questions_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE product_reviews ADD CONSTRAINT FK_product_reviews_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE product_reviews ADD CONSTRAINT FK_product_reviews_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE product_stocks ADD CONSTRAINT FK_product_stocks_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE recently_viewed_products ADD CONSTRAINT FK_recently_viewed_products_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE recently_viewed_products ADD CONSTRAINT FK_recently_viewed_products_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE return_requests ADD CONSTRAINT FK_return_requests_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE return_requests ADD CONSTRAINT FK_return_requests_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE return_requests ADD CONSTRAINT FK_return_requests_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE review_helpful_votes ADD CONSTRAINT FK_review_helpful_votes_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE security_events ADD CONSTRAINT FK_security_events_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE shipments ADD CONSTRAINT FK_shipments_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE size_guide_entries ADD CONSTRAINT FK_size_guide_entries_category_id FOREIGN KEY (category_id) REFERENCES categories(id);
ALTER TABLE stock_movements ADD CONSTRAINT FK_stock_movements_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE stock_notification_requests ADD CONSTRAINT FK_stock_notification_requests_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE stock_reservations ADD CONSTRAINT FK_stock_reservations_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE stock_reservations ADD CONSTRAINT FK_stock_reservations_product_id FOREIGN KEY (product_id) REFERENCES products(id);
ALTER TABLE store_credit_transactions ADD CONSTRAINT FK_store_credit_transactions_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE store_credit_transactions ADD CONSTRAINT FK_store_credit_transactions_order_id FOREIGN KEY (order_id) REFERENCES orders(id);
ALTER TABLE sub_categories ADD CONSTRAINT FK_sub_categories_category_id FOREIGN KEY (category_id) REFERENCES categories(id);
ALTER TABLE user_sessions ADD CONSTRAINT FK_user_sessions_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE wishlist_items ADD CONSTRAINT FK_wishlist_items_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id);
ALTER TABLE wishlist_items ADD CONSTRAINT FK_wishlist_items_product_id FOREIGN KEY (product_id) REFERENCES products(id);

-- === Sik sorgulanan kolonlarda index ===
CREATE INDEX IX_addresses_customer_id ON addresses(customer_id);
CREATE INDEX IX_audit_logs_entity_id ON audit_logs(entity_id);
CREATE INDEX IX_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX IX_carts_customer_id ON carts(customer_id);
CREATE INDEX IX_cart_items_cart_id ON cart_items(cart_id);
CREATE INDEX IX_cart_items_product_id ON cart_items(product_id);
CREATE INDEX IX_collection_items_collection_id ON collection_items(collection_id);
CREATE INDEX IX_collection_items_product_id ON collection_items(product_id);
CREATE INDEX IX_consent_records_customer_id ON consent_records(customer_id);
CREATE INDEX IX_coupon_usages_coupon_id ON coupon_usages(coupon_id);
CREATE INDEX IX_coupon_usages_customer_id ON coupon_usages(customer_id);
CREATE INDEX IX_coupon_usages_order_id ON coupon_usages(order_id);
CREATE INDEX IX_customer_devices_customer_id ON customer_devices(customer_id);
CREATE INDEX IX_invoices_order_id ON invoices(order_id);
CREATE INDEX IX_invoices_customer_id ON invoices(customer_id);
CREATE INDEX IX_invoices_provider_invoice_id ON invoices(provider_invoice_id);
CREATE INDEX IX_loyalty_transactions_customer_id ON loyalty_transactions(customer_id);
CREATE INDEX IX_loyalty_transactions_order_id ON loyalty_transactions(order_id);
CREATE INDEX IX_orders_customer_id ON orders(customer_id);
CREATE INDEX IX_orders_request_id ON orders(request_id);
CREATE INDEX IX_orders_address_id ON orders(address_id);
CREATE INDEX IX_orders_payment_id ON orders(payment_id);
CREATE INDEX IX_order_items_order_id ON order_items(order_id);
CREATE INDEX IX_order_items_product_id ON order_items(product_id);
CREATE INDEX IX_order_snapshots_order_id ON order_snapshots(order_id);
CREATE INDEX IX_order_snapshots_customer_id ON order_snapshots(customer_id);
CREATE INDEX IX_order_snapshot_items_order_snapshot_id ON order_snapshot_items(order_snapshot_id);
CREATE INDEX IX_order_snapshot_items_product_id ON order_snapshot_items(product_id);
CREATE INDEX IX_order_status_histories_order_id ON order_status_histories(order_id);
CREATE INDEX IX_payments_order_id ON payments(order_id);
CREATE INDEX IX_payments_transaction_id ON payments(transaction_id);
CREATE INDEX IX_payments_conversation_id ON payments(conversation_id);
CREATE INDEX IX_price_drop_subscriptions_product_id ON price_drop_subscriptions(product_id);
CREATE INDEX IX_products_category_id ON products(category_id);
CREATE INDEX IX_products_sub_category_id ON products(sub_category_id);
CREATE INDEX IX_products_variant_group_id ON products(variant_group_id);
CREATE INDEX IX_product_attributes_product_id ON product_attributes(product_id);
CREATE INDEX IX_product_images_product_id ON product_images(product_id);
CREATE INDEX IX_product_questions_product_id ON product_questions(product_id);
CREATE INDEX IX_product_questions_customer_id ON product_questions(customer_id);
CREATE INDEX IX_product_reviews_product_id ON product_reviews(product_id);
CREATE INDEX IX_product_reviews_customer_id ON product_reviews(customer_id);
CREATE INDEX IX_product_stocks_product_id ON product_stocks(product_id);
CREATE INDEX IX_recently_viewed_products_customer_id ON recently_viewed_products(customer_id);
CREATE INDEX IX_recently_viewed_products_product_id ON recently_viewed_products(product_id);
CREATE INDEX IX_return_requests_order_id ON return_requests(order_id);
CREATE INDEX IX_return_requests_customer_id ON return_requests(customer_id);
CREATE INDEX IX_return_requests_product_id ON return_requests(product_id);
CREATE INDEX IX_return_requests_refund_id ON return_requests(refund_id);
CREATE INDEX IX_review_helpful_votes_review_id ON review_helpful_votes(review_id);
CREATE INDEX IX_review_helpful_votes_customer_id ON review_helpful_votes(customer_id);
CREATE INDEX IX_security_events_customer_id ON security_events(customer_id);
CREATE INDEX IX_shipments_order_id ON shipments(order_id);
CREATE INDEX IX_size_guide_entries_category_id ON size_guide_entries(category_id);
CREATE INDEX IX_stock_movements_product_id ON stock_movements(product_id);
CREATE INDEX IX_stock_movements_reference_id ON stock_movements(reference_id);
CREATE INDEX IX_stock_notification_requests_product_id ON stock_notification_requests(product_id);
CREATE INDEX IX_stock_reservations_order_id ON stock_reservations(order_id);
CREATE INDEX IX_stock_reservations_product_id ON stock_reservations(product_id);
CREATE INDEX IX_store_credit_transactions_customer_id ON store_credit_transactions(customer_id);
CREATE INDEX IX_store_credit_transactions_order_id ON store_credit_transactions(order_id);
CREATE INDEX IX_sub_categories_category_id ON sub_categories(category_id);
CREATE INDEX IX_user_sessions_customer_id ON user_sessions(customer_id);
CREATE INDEX IX_wishlist_items_customer_id ON wishlist_items(customer_id);
CREATE INDEX IX_wishlist_items_product_id ON wishlist_items(product_id);