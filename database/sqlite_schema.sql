-- Divisima e-ticaret veritabani (SQLITE) - 43 tablo
-- Entity siniflarindan otomatik uretildi. Kolon adlari entity ile birebir (snake_case).

CREATE TABLE addresses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    title TEXT NOT NULL,
    full_name TEXT NOT NULL,
    phone TEXT NOT NULL,
    city TEXT NOT NULL,
    district TEXT NOT NULL,
    full_address TEXT NOT NULL,
    zip_code TEXT NULL,
    is_default INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE audit_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    table_name TEXT NOT NULL,
    entity_id TEXT NOT NULL,
    action TEXT NOT NULL,
    changes TEXT NULL,
    user_id TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE carts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL,
    reminder_sent_at TEXT NULL
);

CREATE TABLE cart_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    cart_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,
    display_order INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE collections (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,
    collection_type INTEGER NOT NULL,
    curator_name TEXT NULL,
    subtitle TEXT NULL,
    gradient TEXT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE collection_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    collection_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    display_order INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE consent_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NULL,
    consent_type TEXT NOT NULL,
    document_version TEXT NOT NULL,
    granted INTEGER NOT NULL,
    ip_address TEXT NULL,
    user_agent TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE contents (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    slug TEXT NOT NULL,
    title_tr TEXT NOT NULL,
    title_en TEXT NULL,
    body_tr TEXT NOT NULL,
    body_en TEXT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE coupons (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL,
    discount_type INTEGER NOT NULL,
    value NUMERIC NOT NULL,
    min_amount NUMERIC NOT NULL,
    max_discount_amount NUMERIC NULL,
    expire_date TEXT NULL,
    usage_limit INTEGER NOT NULL,
    per_user_limit INTEGER NOT NULL DEFAULT 0,
    used_count INTEGER NOT NULL,
    first_order_only INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL,
    row_version INTEGER NOT NULL
);

CREATE TABLE coupon_usages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    coupon_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    order_id INTEGER NOT NULL,
    discount_applied NUMERIC NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE customers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    user_type INTEGER NOT NULL DEFAULT 2,
    phone TEXT NOT NULL,
    address TEXT NULL,
    city TEXT NULL,
    gender TEXT NULL,
    password_salt BLOB NOT NULL,
    password_hash BLOB NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL,
    last_login_at TEXT NULL,
    email_verified INTEGER NOT NULL,
    email_verification_token TEXT NULL,
    email_verification_sent_at TEXT NULL,
    password_reset_token TEXT NULL,
    password_reset_expiry TEXT NULL,
    two_factor_enabled INTEGER NOT NULL,
    two_factor_secret TEXT NULL,
    two_factor_code TEXT NULL,
    two_factor_code_expiry TEXT NULL,
    failed_login_attempts INTEGER NOT NULL,
    lockout_end TEXT NULL,
    birthdate TEXT NULL,
    notify_email INTEGER NOT NULL DEFAULT 1,
    notify_sms INTEGER NOT NULL DEFAULT 1,
    notify_push INTEGER NOT NULL DEFAULT 1,
    loyalty_points INTEGER NOT NULL,
    store_credit NUMERIC NOT NULL,
    referral_code TEXT NULL,
    referred_by INTEGER NULL,
    last_order_at TEXT NULL,
    last_winback_sent_at TEXT NULL,
    birthday_offer_sent_year TEXT NULL
);

CREATE TABLE customer_devices (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    device_token TEXT NOT NULL,
    platform INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    last_used_at TEXT NULL
);

CREATE TABLE gift_cards (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL,
    initial_amount NUMERIC NOT NULL,
    balance NUMERIC NOT NULL,
    is_active INTEGER NOT NULL,
    redeemed_by INTEGER NULL,
    created_at TEXT NOT NULL,
    redeemed_at TEXT NULL
);

CREATE TABLE invoices (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    invoice_number TEXT NOT NULL,
    invoice_type INTEGER NOT NULL,
    tax_number TEXT NULL,
    company_name TEXT NULL,
    subtotal NUMERIC NOT NULL,
    tax_rate NUMERIC NOT NULL,
    tax_amount NUMERIC NOT NULL,
    total NUMERIC NOT NULL,
    status INTEGER NOT NULL,
    provider_invoice_id TEXT NULL,
    pdf_url TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE loyalty_transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    points INTEGER NOT NULL,
    type INTEGER NOT NULL,
    reason TEXT NOT NULL,
    order_id INTEGER NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    order_number TEXT NOT NULL,
    request_id TEXT NULL,
    status INTEGER NOT NULL,
    subtotal NUMERIC NOT NULL,
    discount_amount NUMERIC NOT NULL,
    shipping_cost NUMERIC NOT NULL,
    total_price NUMERIC NOT NULL,
    currency TEXT NOT NULL DEFAULT 'TRY',
    coupon_code TEXT NULL,
    address_id INTEGER NULL,
    payment_type INTEGER NOT NULL,
    store_credit_used NUMERIC NOT NULL DEFAULT 0,
    installment_count INTEGER NOT NULL DEFAULT 1,
    is_online_payment_done INTEGER NOT NULL,
    payment_id TEXT NULL,
    created_at TEXT NOT NULL,
    delivered_at TEXT NULL,
    review_invite_sent_at TEXT NULL
);

CREATE TABLE order_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    unit_price NUMERIC NOT NULL,
    is_cancelled INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE order_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    customer_full_name TEXT NOT NULL,
    shipping_address TEXT NULL,
    status INTEGER NOT NULL,
    subtotal NUMERIC NOT NULL,
    discount_amount NUMERIC NOT NULL,
    shipping_cost NUMERIC NOT NULL,
    total_price NUMERIC NOT NULL,
    coupon_code TEXT NULL,
    snapshot_created_at TEXT NOT NULL,
    order_created_at TEXT NOT NULL
);

CREATE TABLE order_snapshot_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_snapshot_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    product_name TEXT NOT NULL,
    brand TEXT NOT NULL,
    product_price NUMERIC NOT NULL,
    size TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE order_status_histories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    status INTEGER NOT NULL,
    note TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE outbox_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_type TEXT NOT NULL,
    payload TEXT NOT NULL,
    status INTEGER NOT NULL,
    retry_count INTEGER NOT NULL,
    error TEXT NULL,
    created_at TEXT NOT NULL,
    processed_at TEXT NULL
);

CREATE TABLE payments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    payment_provider TEXT NOT NULL,
    payment_status INTEGER NOT NULL,
    amount NUMERIC NOT NULL,
    paid_price NUMERIC NULL,
    installment_count INTEGER NOT NULL DEFAULT 1,
    installment_fee NUMERIC NULL,
    currency TEXT NULL,
    fraud_status TEXT NULL,
    transaction_id TEXT NULL,
    conversation_id TEXT NULL,
    token TEXT NULL,
    paid_at TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE price_drop_subscriptions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    email TEXT NOT NULL,
    subscribed_price NUMERIC NOT NULL,
    is_notified INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    notified_at TEXT NULL
);

CREATE TABLE products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    brand TEXT NOT NULL,
    category_id INTEGER NOT NULL,
    sub_category_id INTEGER NULL,
    price NUMERIC NOT NULL,
    sale_price NUMERIC NULL,
    sale_start TEXT NULL,
    sale_end TEXT NULL,
    old_price NUMERIC NULL,
    description TEXT NOT NULL,
    color_hex TEXT NOT NULL,
    variant_group_id TEXT NULL,
    image_url TEXT NULL,
    product_type INTEGER NOT NULL,
    average_rating NUMERIC NOT NULL DEFAULT 0,
    review_count INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE product_attributes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    attribute_key TEXT NOT NULL,
    attribute_value TEXT NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE product_images (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    image_url TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    is_primary INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE product_questions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    question TEXT NOT NULL,
    answer TEXT NULL,
    answered_by INTEGER NULL,
    is_answered INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    answered_at TEXT NULL
);

CREATE TABLE product_reviews (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    rating INTEGER NOT NULL,
    comment TEXT NOT NULL,
    is_verified_purchase INTEGER NOT NULL,
    helpful_count INTEGER NOT NULL,
    review_status INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE product_stocks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    stock_quantity INTEGER NOT NULL,
    reserved_quantity INTEGER NOT NULL,
    row_version INTEGER NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE recently_viewed_products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    viewed_at TEXT NOT NULL
);

CREATE TABLE return_requests (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    reason INTEGER NOT NULL,
    description TEXT NULL,
    return_type INTEGER NOT NULL,
    status INTEGER NOT NULL,
    refund_amount NUMERIC NOT NULL,
    refund_id TEXT NULL,
    admin_note TEXT NULL,
    created_at TEXT NOT NULL,
    processed_at TEXT NULL
);

CREATE TABLE review_helpful_votes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    review_id INTEGER NOT NULL,
    customer_id INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE security_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_type TEXT NOT NULL,
    severity TEXT NOT NULL,
    customer_id INTEGER NULL,
    ip_address TEXT NULL,
    user_agent TEXT NULL,
    detail TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE shipments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    carrier INTEGER NOT NULL,
    tracking_number TEXT NOT NULL,
    status INTEGER NOT NULL,
    last_status_text TEXT NULL,
    shipped_at TEXT NULL,
    estimated_delivery TEXT NULL,
    delivered_at TEXT NULL,
    created_at TEXT NOT NULL,
    last_checked_at TEXT NULL
);

CREATE TABLE size_guide_entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL,
    size_label TEXT NOT NULL,
    bust_cm NUMERIC NULL,
    waist_cm NUMERIC NULL,
    hip_cm NUMERIC NULL,
    length_cm NUMERIC NULL,
    is_active INTEGER NOT NULL,
    sort_order INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE stock_movements (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    movement_type INTEGER NOT NULL,
    quantity INTEGER NOT NULL,
    reference_id INTEGER NULL,
    note TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE stock_notification_requests (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    email TEXT NOT NULL,
    is_notified INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    notified_at TEXT NULL
);

CREATE TABLE stock_reservations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    size TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    status INTEGER NOT NULL,
    expires_at TEXT NOT NULL,
    created_at TEXT NOT NULL,
    closed_at TEXT NULL
);

CREATE TABLE store_credit_transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    amount NUMERIC NOT NULL,
    type INTEGER NOT NULL,
    reason TEXT NOT NULL,
    order_id INTEGER NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE sub_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    slug TEXT NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE user_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    refresh_token TEXT NOT NULL,
    device TEXT NULL,
    ip_address TEXT NULL,
    expires_at TEXT NOT NULL,
    is_active INTEGER NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE wishlist_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    created_at TEXT NOT NULL
);
