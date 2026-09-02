using Divisima.Core.Security.Encryption;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.Context
{
    // Açıklayıcı yorum: Divisima EF Core DbContext'i. Cafixo CafixoContext kalıbı.
    // NOT: Bu dosya Product modülü için kurgulandı; diğer modüller eklendikçe DbSet + config genişleyecek.
    public class DivisimaDbContext : DbContext
    {
        private readonly IEncryptionProvider _encryption;

        public DivisimaDbContext(DbContextOptions<DivisimaDbContext> options) : base(options) { }

        // Açıklayıcı yorum: Field-level encryption için provider'lı ctor (DI bunu kullanır)
        public DivisimaDbContext(DbContextOptions<DivisimaDbContext> options, IEncryptionProvider encryption) : base(options)
        {
            _encryption = encryption;
        }

        // Açıklayıcı yorum: DbSet'ler (her entity için tablo)
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductStock> ProductStocks { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsage> CouponUsages { get; set; }
        public DbSet<Collection> Collections { get; set; }
        public DbSet<CollectionItem> CollectionItems { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Seller> Sellers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderSnapshot> OrderSnapshots { get; set; }
        public DbSet<OrderSnapshotItem> OrderSnapshotItems { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<Content> Contents { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SecurityEvent> SecurityEvents { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<CustomerDevice> CustomerDevices { get; set; }
        public DbSet<Shipment> Shipments { get; set; }
        public DbSet<StockReservation> StockReservations { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<StockNotificationRequest> StockNotificationRequests { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
        public DbSet<RecentlyViewedProduct> RecentlyViewedProducts { get; set; }
        public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }
        public DbSet<StoreCreditTransaction> StoreCreditTransactions { get; set; }
        public DbSet<GiftCard> GiftCards { get; set; }
        public DbSet<ConsentRecord> ConsentRecords { get; set; }
        public DbSet<PriceDropSubscription> PriceDropSubscriptions { get; set; }
        public DbSet<ReviewHelpfulVote> ReviewHelpfulVotes { get; set; }
        public DbSet<ProductQuestion> ProductQuestions { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; }
        public DbSet<SizeGuideEntry> SizeGuideEntries { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        // public DbSet<ProductReview> ProductReviews { get; set; }   // sonraki modülde

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Varsayilan ondalik tipi. OnModelCreating'deki acik HasColumnType bunu ezer.
            configurationBuilder.Properties<decimal>().HaveColumnType("decimal(18,2)");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Açıklayıcı yorum: Product tablo + kolon konfigürasyonu (snake_case kolon isimleri)
            modelBuilder.Entity<Product>(b =>
            {
                b.ToTable("products");
                b.HasKey(p => p.id);

                b.Property(p => p.id).HasColumnName("id");
                b.Property(p => p.name).HasColumnName("name").IsRequired().HasMaxLength(200);
                b.Property(p => p.brand).HasColumnName("brand").HasMaxLength(120);
                b.Property(p => p.category_id).HasColumnName("category_id");
                b.Property(p => p.sub_category_id).HasColumnName("sub_category_id");
                b.Property(p => p.price).HasColumnName("price").HasColumnType("decimal(18,2)");
                b.Property(p => p.sale_price).HasColumnName("sale_price").HasColumnType("decimal(18,2)");
                b.Property(p => p.sale_start).HasColumnName("sale_start");
                b.Property(p => p.sale_end).HasColumnName("sale_end");
                b.Property(p => p.old_price).HasColumnName("old_price").HasColumnType("decimal(18,2)");
                b.Property(p => p.description).HasColumnName("description");
                b.Property(p => p.color_hex).HasColumnName("color_hex").HasMaxLength(9);
                b.Property(p => p.variant_group_id).HasColumnName("variant_group_id").HasMaxLength(50);
                b.HasIndex(p => p.variant_group_id);
                b.Property(p => p.image_url).HasColumnName("image_url").HasMaxLength(1000);
                b.Property(p => p.product_type).HasColumnName("product_type");
                b.Property(p => p.vat_rate).HasColumnName("vat_rate").HasColumnType("decimal(5,4)");
                b.Property(p => p.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(p => p.created_at).HasColumnName("created_at");
                b.Property(p => p.updated_at).HasColumnName("updated_at");

                // Açıklayıcı yorum: İlişkiler - kategori (zorunlu), alt kategori (opsiyonel), beden stokları



                // Açıklayıcı yorum: Sık filtrelenen kolonlara index (kategori + aktiflik)
                b.HasIndex(p => p.category_id);
                b.HasIndex(p => p.is_active);
                b.Property(p => p.seller_id).HasColumnName("seller_id");
                b.HasIndex(p => p.seller_id); // satıcı bazlı ürün sorgusu (marketplace izolasyonu)
            });

            // Açıklayıcı yorum: ProductStock tablo + kolon konfigürasyonu
            modelBuilder.Entity<ProductStock>(b =>
            {
                b.ToTable("product_stocks");
                b.HasKey(s => s.id);

                b.Property(s => s.id).HasColumnName("id");
                b.Property(s => s.product_id).HasColumnName("product_id");
                b.Property(s => s.size).HasColumnName("size").HasMaxLength(10);
                b.Property(s => s.stock_quantity).HasColumnName("stock_quantity");
                b.Property(s => s.reserved_quantity).HasColumnName("reserved_quantity").HasDefaultValue(0);
                // Açıklayıcı yorum: SQL Server rowversion - satır her güncellendiğinde otomatik değişir
                b.Property(s => s.row_version).HasColumnName("row_version").IsRowVersion();
                b.Property(s => s.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(s => s.created_at).HasColumnName("created_at");
                b.Property(s => s.updated_at).HasColumnName("updated_at");

                // Açıklayıcı yorum: Aynı ürün + beden tek satır olmalı (benzersiz index)
                b.HasIndex(s => new { s.product_id, s.size }).IsUnique();

                // ══ DALGA D / D2 - REFERANS BUTUNLUGU DB'DE ═══════════════════════════════
                //
                // OLCULEN ONCE-DURUM: product_stocks -> products FK'si YOKTU. Dev veritabaninda
                // 120 YETIM satir vardi (40 ayri product_id, 3..182) - Dalga 3'un performans
                // seed temizligi urun satirlarini DOGRUDAN silmis, stok satirlarini birakmisti.
                //
                // "Bugun uretimde fiziksel silme yolu yok" (ProductManager.Delete SOFT-delete)
                // bunu ONLEMEYE YETMEZ - kullanici karari: yarin da olmayacagi anlamina gelmiyor
                // ve bir pin kirildiginda hasar COKTAN olusmus olur. Ayni tabloda bu gece zaten
                // bir kez "kimse buraya dokunmaz" varsayiminin bedeli odendi (filtresiz UNIQUE
                // indeks -> urunun TUM bedenlerini kaybettiren guncelleme, Dalga B).
                //
                // SILME DAVRANISI: Restrict (SQL Server'da ON DELETE NO ACTION) - OLCUMLE SECILDI.
                // products'a isaret eden MEVCUT iki FK de (product_reviews, order_items)
                // NO_ACTION; yani deponun kendi konvansiyonu zaten "silmeyi ENGELLE".
                // CASCADE REDDEDILDI: uretimde silme SOFT oldugu icin cascade normal isleyiste
                // HIC atesLENMEZ - yalnizca TEHLIKELI durumda (dogrudan SQL ile fiziksel silme)
                // atesLENIR ve tam da durdurulmasi gereken anda stok gecmisini SESSIZCE goturur.
                //
                // NAVIGATION EKLENMEDI: ProductStock duz bir entity (yalniz product_id tasiyor)
                // ve oyle kalmali - iliski model duzeyinde navigasyonsuz tanimlanabiliyor.
                // ProductStock'ta is_active global filtresi YOK (bilincli - gerekce asagidaki
                // "B1: Soft-delete global filtre genisletme" blogunda, ALTI entity'lik dislama
                // listesinde), Product'ta VAR; FK yalnizca INSERT/DELETE'i DB duzeyinde baglar,
                // sorgulari etkilemez.
                b.HasOne<Product>()
                 .WithMany()
                 .HasForeignKey(s => s.product_id)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_product_stocks_product_id");   // ad SEMA DOSYASIYLA ayni - gerekce migration'da
            });

            // Açıklayıcı yorum: Category tablo + kolon konfigürasyonu
            modelBuilder.Entity<Category>(b =>
            {
                b.ToTable("categories");
                b.HasKey(c => c.id);

                b.Property(c => c.id).HasColumnName("id");
                b.Property(c => c.name).HasColumnName("name").IsRequired().HasMaxLength(100);
                b.Property(c => c.slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
                b.Property(c => c.display_order).HasColumnName("display_order");
                b.Property(c => c.vat_rate).HasColumnName("vat_rate").HasColumnType("decimal(5,4)");
                b.Property(c => c.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(c => c.created_at).HasColumnName("created_at");
                b.Property(c => c.updated_at).HasColumnName("updated_at");

                // Açıklayıcı yorum: Slug benzersiz olmalı (frontend cat key)
                b.HasIndex(c => c.slug).IsUnique();
            });

            // Açıklayıcı yorum: SubCategory tablo + kolon konfigürasyonu
            modelBuilder.Entity<SubCategory>(b =>
            {
                b.ToTable("sub_categories");
                b.HasKey(s => s.id);

                b.Property(s => s.id).HasColumnName("id");
                b.Property(s => s.category_id).HasColumnName("category_id");
                b.Property(s => s.name).HasColumnName("name").IsRequired().HasMaxLength(100);
                b.Property(s => s.slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
                b.Property(s => s.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(s => s.created_at).HasColumnName("created_at");
                b.Property(s => s.updated_at).HasColumnName("updated_at");

                // Açıklayıcı yorum: Alt kategori -> kategori ilişkisi

                b.HasIndex(s => new { s.category_id, s.slug }).IsUnique();
            });

            // Açıklayıcı yorum: Coupon tablo + kolon konfigürasyonu
            modelBuilder.Entity<Coupon>(b =>
            {
                b.ToTable("coupons");
                b.HasKey(c => c.id);
                b.Property(c => c.id).HasColumnName("id");
                b.Property(c => c.code).HasColumnName("code").IsRequired().HasMaxLength(40);
                b.Property(c => c.discount_type).HasColumnName("discount_type");
                b.Property(c => c.value).HasColumnName("value").HasColumnType("decimal(18,2)");
                b.Property(c => c.min_amount).HasColumnName("min_amount").HasColumnType("decimal(18,2)");
                b.Property(c => c.max_discount_amount).HasColumnName("max_discount_amount").HasColumnType("decimal(18,2)");
                b.Property(c => c.expire_date).HasColumnName("expire_date");
                b.Property(c => c.usage_limit).HasColumnName("usage_limit");
                b.Property(c => c.used_count).HasColumnName("used_count");
                b.Property(c => c.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(c => c.first_order_only).HasColumnName("first_order_only").HasDefaultValue(false);
                b.Property(c => c.row_version).HasColumnName("row_version").IsRowVersion();
                b.Property(c => c.created_at).HasColumnName("created_at");
                b.Property(c => c.updated_at).HasColumnName("updated_at");
                // Açıklayıcı yorum: Kupon kodu benzersiz
                b.HasIndex(c => c.code).IsUnique();
            });

            // Açıklayıcı yorum: CouponUsage tablo + kolon konfigürasyonu (kullanım takibi)
            modelBuilder.Entity<CouponUsage>(b =>
            {
                b.ToTable("coupon_usages");
                b.HasKey(u => u.id);
                b.Property(u => u.id).HasColumnName("id");
                b.Property(u => u.coupon_id).HasColumnName("coupon_id");
                b.Property(u => u.customer_id).HasColumnName("customer_id");
                b.Property(u => u.order_id).HasColumnName("order_id");
                b.Property(u => u.discount_applied).HasColumnName("discount_applied").HasColumnType("decimal(18,2)");
                b.Property(u => u.created_at).HasColumnName("created_at");

                // SPRINT 8 MADDE 1 - IKINCI SAVUNMA HATTI.
                // used_count artik bu tablodan TURETILIYOR; dolayisiyla sayacin dogrulugu
                // "ayni siparis icin iki kullanim satiri olusamaz" garantisine baglandi.
                // Uygulama katmani zaten satiri transaction icinde yaziyor, ama at-least-once
                // bir yeniden deneme (outbox - madde 3) ya da paralel bir callback ayni siparis
                // icin ikinci satiri deneyebilir. Veritabani duzeyinde ENGELLENIR.
                // (Sprint 6'daki UX_loyalty_transactions_order_earn ile ayni kalip.)
                b.HasIndex(u => new { u.coupon_id, u.order_id })
                    .IsUnique()
                    .HasDatabaseName("UX_coupon_usages_coupon_order");
            });

            // Açıklayıcı yorum: Collection tablo + kolon konfigürasyonu
            modelBuilder.Entity<Collection>(b =>
            {
                b.ToTable("collections");
                b.HasKey(c => c.id);
                b.Property(c => c.id).HasColumnName("id");
                b.Property(c => c.name).HasColumnName("name").IsRequired().HasMaxLength(150);
                b.Property(c => c.slug).HasColumnName("slug").IsRequired().HasMaxLength(150);
                b.Property(c => c.collection_type).HasColumnName("collection_type");
                b.Property(c => c.curator_name).HasColumnName("curator_name").HasMaxLength(120);
                b.Property(c => c.subtitle).HasColumnName("subtitle").HasMaxLength(300);
                b.Property(c => c.gradient).HasColumnName("gradient").HasMaxLength(200);
                b.Property(c => c.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(c => c.created_at).HasColumnName("created_at");
                b.Property(c => c.updated_at).HasColumnName("updated_at");
                b.HasIndex(c => c.slug).IsUnique();
            });

            // Açıklayıcı yorum: CollectionItem tablo (koleksiyon-ürün many-to-many)
            modelBuilder.Entity<CollectionItem>(b =>
            {
                b.ToTable("collection_items");
                b.HasKey(i => i.id);
                b.Property(i => i.id).HasColumnName("id");
                b.Property(i => i.collection_id).HasColumnName("collection_id");
                b.Property(i => i.product_id).HasColumnName("product_id");
                b.Property(i => i.display_order).HasColumnName("display_order");
                b.Property(i => i.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(i => i.created_at).HasColumnName("created_at");
                b.HasIndex(i => new { i.collection_id, i.product_id }).IsUnique();
            });

            // Açıklayıcı yorum: Customer tablo konfigürasyonu (şifre hash+salt byte[])
            modelBuilder.Entity<Customer>(b =>
            {
                b.ToTable("customers");
                b.HasKey(c => c.id);
                b.Property(c => c.id).HasColumnName("id");
                b.Property(c => c.email).HasColumnName("email").IsRequired().HasMaxLength(200);
                b.Property(c => c.phone).HasColumnName("phone").HasMaxLength(20);
                b.Property(c => c.password_hash).HasColumnName("password_hash");
                b.Property(c => c.password_salt).HasColumnName("password_salt");
                b.Property(c => c.gender).HasColumnName("gender");
                b.Property(c => c.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(c => c.created_at).HasColumnName("created_at");
                b.Property(c => c.updated_at).HasColumnName("updated_at");
                b.Property(c => c.email_verified).HasColumnName("email_verified").HasDefaultValue(false);
                b.Property(c => c.email_verification_token).HasColumnName("email_verification_token").HasMaxLength(120);
                b.Property(c => c.email_verification_sent_at).HasColumnName("email_verification_sent_at");
                b.Property(c => c.password_reset_token).HasColumnName("password_reset_token").HasMaxLength(120);
                b.Property(c => c.password_reset_expiry).HasColumnName("password_reset_expiry");
                b.Property(c => c.two_factor_enabled).HasColumnName("two_factor_enabled").HasDefaultValue(false);
                var tfSecret = b.Property(c => c.two_factor_secret).HasColumnName("two_factor_secret").HasMaxLength(400);
                // Açıklayıcı yorum: 2FA gizli anahtarı DB'de AES ile şifreli tutulur (DB sızsa bile okunamaz)
                if (_encryption != null) tfSecret.HasConversion(new EncryptedConverter(_encryption));
                b.Property(c => c.failed_login_attempts).HasColumnName("failed_login_attempts");
                b.Property(c => c.lockout_end).HasColumnName("lockout_end");
                b.Property(c => c.birthdate).HasColumnName("birthdate");
                b.Property(c => c.notify_email).HasColumnName("notify_email").HasDefaultValue(true);
                b.Property(c => c.notify_sms).HasColumnName("notify_sms").HasDefaultValue(true);
                b.Property(c => c.notify_push).HasColumnName("notify_push").HasDefaultValue(true);
                b.Property(c => c.loyalty_points).HasColumnName("loyalty_points").HasDefaultValue(0);
                b.Property(c => c.store_credit).HasColumnName("store_credit").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(c => c.referral_code).HasColumnName("referral_code").HasMaxLength(20);
                b.Property(c => c.referred_by).HasColumnName("referred_by");
                b.Property(c => c.last_order_at).HasColumnName("last_order_at");
                b.Property(c => c.last_winback_sent_at).HasColumnName("last_winback_sent_at");
                b.Property(c => c.birthday_offer_sent_year).HasColumnName("birthday_offer_sent_year");
                b.HasIndex(c => c.email).IsUnique();
            });

            // ── Satıcı (marketplace vendor) ──
            modelBuilder.Entity<Seller>(sb =>
            {
                sb.ToTable("sellers");
                sb.HasKey(s => s.id);
                sb.Property(s => s.id).HasColumnName("id");
                sb.Property(s => s.business_name).HasColumnName("business_name").IsRequired().HasMaxLength(200);
                sb.Property(s => s.email).HasColumnName("email").IsRequired().HasMaxLength(200);
                sb.Property(s => s.user_type).HasColumnName("user_type").HasDefaultValue((byte)3);
                sb.Property(s => s.password_hash).HasColumnName("password_hash");
                sb.Property(s => s.password_salt).HasColumnName("password_salt");
                sb.Property(s => s.phone).HasColumnName("phone").HasMaxLength(20);
                sb.Property(s => s.tax_number).HasColumnName("tax_number").HasMaxLength(30);
                sb.Property(s => s.status).HasColumnName("status").HasDefaultValue((byte)0);
                sb.Property(s => s.commission_rate).HasColumnName("commission_rate").HasColumnType("decimal(5,2)").HasDefaultValue(10m);
                sb.Property(s => s.is_active).HasColumnName("is_active").HasDefaultValue(true);
                sb.Property(s => s.failed_login_attempts).HasColumnName("failed_login_attempts");
                sb.Property(s => s.lockout_end).HasColumnName("lockout_end");
                sb.Property(s => s.created_at).HasColumnName("created_at");
                sb.Property(s => s.updated_at).HasColumnName("updated_at");
                sb.HasIndex(s => s.email).IsUnique();
                sb.HasIndex(s => s.status);
            });

            // Açıklayıcı yorum: Order tablo konfigürasyonu (tutar alanları decimal)
            modelBuilder.Entity<Order>(b =>
            {
                b.ToTable("orders");
                b.HasKey(o => o.id);
                b.Property(o => o.id).HasColumnName("id");
                b.Property(o => o.customer_id).HasColumnName("customer_id");
                b.Property(o => o.order_number).HasColumnName("order_number").IsRequired().HasMaxLength(40);
                b.Property(o => o.request_id).HasColumnName("request_id").HasMaxLength(80);
                b.Property(o => o.status).HasColumnName("status");
                b.Property(o => o.payment_type).HasColumnName("payment_type");
                b.Property(o => o.is_online_payment_done).HasColumnName("is_online_payment_done");
                b.Property(o => o.payment_id).HasColumnName("payment_id").HasMaxLength(120);
                b.Property(o => o.subtotal).HasColumnName("subtotal").HasColumnType("decimal(18,2)");
                b.Property(o => o.discount_amount).HasColumnName("discount_amount").HasColumnType("decimal(18,2)");
                b.Property(o => o.shipping_cost).HasColumnName("shipping_cost").HasColumnType("decimal(18,2)");
                b.Property(o => o.total_price).HasColumnName("total_price").HasColumnType("decimal(18,2)");
                // Kumulatif iade sayaci - varsayilan 0 (mevcut satirlar icin de).
                b.Property(o => o.refunded_amount).HasColumnName("refunded_amount")
                    .HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(o => o.currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("TRY");
                b.Property(o => o.coupon_code).HasColumnName("coupon_code").HasMaxLength(40);
                b.Property(o => o.address_id).HasColumnName("address_id");
                b.Property(o => o.created_at).HasColumnName("created_at");
                b.Property(o => o.review_invite_sent_at).HasColumnName("review_invite_sent_at");
                b.HasIndex(o => o.order_number).IsUnique();
                // Concurrency: request_id UNIQUE (filtered - nullable olduğu için yalnız NOT NULL) - idempotency
                // check-then-act TOCTOU race'inde iki eşzamanlı istek aynı request_id ile ÇİFT sipariş yaratamaz.
                b.HasIndex(o => o.request_id).IsUnique().HasFilter("[request_id] IS NOT NULL");
                b.HasIndex(o => o.customer_id);
            });

            // Açıklayıcı yorum: OrderItem tablo konfigürasyonu
            modelBuilder.Entity<OrderItem>(b =>
            {
                b.ToTable("order_items");
                b.HasKey(i => i.id);
                b.Property(i => i.id).HasColumnName("id");
                b.Property(i => i.order_id).HasColumnName("order_id");
                b.Property(i => i.product_id).HasColumnName("product_id");
                b.Property(i => i.size).HasColumnName("size").HasMaxLength(10);
                b.Property(i => i.quantity).HasColumnName("quantity");
                b.Property(i => i.unit_price).HasColumnName("unit_price").HasColumnType("decimal(18,2)");
                b.Property(i => i.is_cancelled).HasColumnName("is_cancelled").HasDefaultValue(false);
                b.Property(i => i.seller_id).HasColumnName("seller_id");
                b.Property(i => i.created_at).HasColumnName("created_at");
                b.HasIndex(i => i.seller_id); // satıcı bazlı satış sorgusu (marketplace)
            });

            // Açıklayıcı yorum: OrderSnapshot tablo konfigürasyonu (sipariş anı dondurma)
            modelBuilder.Entity<OrderSnapshot>(b =>
            {
                b.ToTable("order_snapshots");
                b.HasKey(sn => sn.id);
                b.Property(sn => sn.id).HasColumnName("id");
                b.Property(sn => sn.order_id).HasColumnName("order_id");
                b.Property(sn => sn.customer_full_name).HasColumnName("customer_full_name").HasMaxLength(200);
                b.Property(sn => sn.shipping_address).HasColumnName("shipping_address").HasMaxLength(500);
                b.Property(sn => sn.subtotal).HasColumnName("subtotal").HasColumnType("decimal(18,2)");
                b.Property(sn => sn.discount_amount).HasColumnName("discount_amount").HasColumnType("decimal(18,2)");
                b.Property(sn => sn.shipping_cost).HasColumnName("shipping_cost").HasColumnType("decimal(18,2)");
                b.Property(sn => sn.coupon_code).HasColumnName("coupon_code").HasMaxLength(40);
            });

            // Açıklayıcı yorum: OrderSnapshotItem tablo konfigürasyonu
            modelBuilder.Entity<OrderSnapshotItem>(b =>
            {
                b.ToTable("order_snapshot_items");
                b.HasKey(si => si.id);
                b.Property(si => si.id).HasColumnName("id");
                b.Property(si => si.order_snapshot_id).HasColumnName("order_snapshot_id");
                b.Property(si => si.product_id).HasColumnName("product_id");
                b.Property(si => si.product_name).HasColumnName("product_name").HasMaxLength(200);
                b.Property(si => si.brand).HasColumnName("brand").HasMaxLength(120);
                b.Property(si => si.size).HasColumnName("size").HasMaxLength(10);
                b.Property(si => si.quantity).HasColumnName("quantity");
                b.Property(si => si.created_at).HasColumnName("created_at");
            });

            // Açıklayıcı yorum: StockMovement tablo konfigürasyonu (stok hareketleri)
            modelBuilder.Entity<StockMovement>(b =>
            {
                b.ToTable("stock_movements");
                b.HasKey(m => m.id);
                b.Property(m => m.id).HasColumnName("id");
                b.Property(m => m.product_id).HasColumnName("product_id");
                b.Property(m => m.size).HasColumnName("size").HasMaxLength(10);
                b.Property(m => m.movement_type).HasColumnName("movement_type");
                b.Property(m => m.quantity).HasColumnName("quantity");
                b.Property(m => m.reference_id).HasColumnName("reference_id");
                b.Property(m => m.note).HasColumnName("note").HasMaxLength(200);
                b.Property(m => m.created_at).HasColumnName("created_at");
                b.HasIndex(m => new { m.product_id, m.size });
            });

            // Açıklayıcı yorum: ProductReview tablo konfigürasyonu (onay akışlı yorum)
            modelBuilder.Entity<ProductReview>(b =>
            {
                b.ToTable("product_reviews");
                b.HasKey(r => r.id);
                b.Property(r => r.id).HasColumnName("id");
                b.Property(r => r.product_id).HasColumnName("product_id");
                b.Property(r => r.customer_id).HasColumnName("customer_id");
                b.Property(r => r.rating).HasColumnName("rating");
                b.Property(r => r.comment).HasColumnName("comment").HasMaxLength(1000);
                b.Property(r => r.review_status).HasColumnName("review_status");
                b.Property(r => r.is_verified_purchase).HasColumnName("is_verified_purchase").HasDefaultValue(false);
                b.Property(r => r.helpful_count).HasColumnName("helpful_count").HasDefaultValue(0);
                b.Property(r => r.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(r => r.created_at).HasColumnName("created_at");
                b.Property(r => r.updated_at).HasColumnName("updated_at");
                b.HasIndex(r => r.product_id);
            });

            // Açıklayıcı yorum: UserSession tablo konfigürasyonu
            modelBuilder.Entity<UserSession>(b =>
            {
                b.ToTable("user_sessions");
                b.HasKey(u => u.id);
                b.Property(u => u.id).HasColumnName("id");
                b.Property(u => u.customer_id).HasColumnName("customer_id");
                b.Property(u => u.refresh_token).HasColumnName("refresh_token").HasMaxLength(500);
                b.Property(u => u.device).HasColumnName("device").HasMaxLength(200);
                b.Property(u => u.ip_address).HasColumnName("ip_address").HasMaxLength(64);
                b.Property(u => u.expires_at).HasColumnName("expires_at");
                b.Property(u => u.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(u => u.created_at).HasColumnName("created_at");
                // GF-1 / K3: oturum zincirinin GIRIS ani. NULL BIRAKILIR - GF-1 oncesi satirlar
                // icin geriye donuk doldurma YAPILMAZ (davranis statuko kalir). Gerekce entity'de.
                b.Property(u => u.auth_time).HasColumnName("auth_time");
                // Not: UserSession entity'sinde token/updated_at ALANI YOK -> map EDILMEZ (aksi halde CS1061 derleme hatasi).
                b.HasIndex(u => u.customer_id);
                // ══ GF-1b / K3 - FILTRELI UNIQUE ══════════════════════════════════════════
                // Kolon artik DUZ JETON degil SHA-256 hex OZET tutuyor. UNIQUE olmasi iki isi
                // birden yapar: (a) ayni ozetin iki satirda bulunmasini ENGELLER, (b) refresh
                // rotasyonunun CAS'ini (K4) veritabani duzeyinde destekler.
                // FILTRELI (merkez karari DUR-3): SQL Server UNIQUE indeksi NULL'lari ESIT
                // sayar; kolon modelde nullable oldugu icin filtresiz bir UNIQUE ileride
                // NULL'lu bir satir olustugunda PATLARDI.
                b.HasIndex(u => u.refresh_token).IsUnique().HasFilter("[refresh_token] IS NOT NULL");
            });

            // Açıklayıcı yorum: Content tablo konfigürasyonu (çok dilli legal sayfalar)
            modelBuilder.Entity<Content>(b =>
            {
                b.ToTable("contents");
                b.HasKey(c => c.id);
                b.Property(c => c.id).HasColumnName("id");
                b.Property(c => c.slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
                b.Property(c => c.title_tr).HasColumnName("title_tr").HasMaxLength(200);
                b.Property(c => c.title_en).HasColumnName("title_en").HasMaxLength(200);
                b.Property(c => c.body_tr).HasColumnName("body_tr");
                b.Property(c => c.body_en).HasColumnName("body_en");
                b.Property(c => c.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(c => c.created_at).HasColumnName("created_at");
                b.Property(c => c.updated_at).HasColumnName("updated_at");
                b.HasIndex(c => c.slug).IsUnique();
            });

            // Açıklayıcı yorum: ProductReview tablo konfigürasyonu

            // Açıklayıcı yorum: Cart tablo konfigürasyonu
            modelBuilder.Entity<Cart>(b =>
            {
                b.ToTable("carts");
                b.HasKey(c => c.id);
                b.Property(c => c.id).HasColumnName("id");
                b.Property(c => c.customer_id).HasColumnName("customer_id");
                b.Property(c => c.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(c => c.created_at).HasColumnName("created_at");
                b.Property(c => c.updated_at).HasColumnName("updated_at");
                b.Property(c => c.reminder_sent_at).HasColumnName("reminder_sent_at");
                b.HasIndex(c => c.customer_id);
            });

            // Açıklayıcı yorum: CartItem tablo konfigürasyonu
            modelBuilder.Entity<CartItem>(b =>
            {
                b.ToTable("cart_items");
                b.HasKey(i => i.id);
                b.Property(i => i.id).HasColumnName("id");
                b.Property(i => i.cart_id).HasColumnName("cart_id");
                b.Property(i => i.product_id).HasColumnName("product_id");
                b.Property(i => i.size).HasColumnName("size").HasMaxLength(10);
                b.Property(i => i.quantity).HasColumnName("quantity");
                b.Property(i => i.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(i => i.created_at).HasColumnName("created_at");
                b.Property(i => i.updated_at).HasColumnName("updated_at");
                b.HasIndex(i => i.cart_id);
                // Concurrency: aktif sepet kalemi (cart_id, product_id, size) FILTERED-UNIQUE - eszamanlı ayni
                // urun+beden ekleme check-then-act race'inde CIFT kalem olusmaz (is_active=1: soft-delete sonrasi izin).
                b.HasIndex(i => new { i.cart_id, i.product_id, i.size }).IsUnique().HasFilter("[is_active] = 1");
            });

            // Açıklayıcı yorum: OutboxMessage tablosu (garantili event)
            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.ToTable("outbox_messages");
                b.HasKey(m => m.id);
                b.Property(m => m.event_type).HasColumnName("event_type").HasMaxLength(100);
                b.Property(m => m.payload).HasColumnName("payload");
                b.Property(m => m.status).HasColumnName("status");
                b.Property(m => m.retry_count).HasColumnName("retry_count");
                b.Property(m => m.error).HasColumnName("error").HasMaxLength(1000);
                b.Property(m => m.created_at).HasColumnName("created_at");
                b.Property(m => m.processed_at).HasColumnName("processed_at");
                // B14: bekleyen mesaj sorgusu için index
                b.HasIndex(m => new { m.status, m.created_at });
            });

            // Açıklayıcı yorum: Payment tablosu
            modelBuilder.Entity<Payment>(b =>
            {
                b.ToTable("payments");
                b.HasKey(p => p.id);
                b.Property(p => p.order_id).HasColumnName("order_id");
                b.Property(p => p.payment_provider).HasColumnName("payment_provider").HasMaxLength(40);
                b.Property(p => p.payment_status).HasColumnName("payment_status");
                b.Property(p => p.amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
                b.Property(p => p.paid_price).HasColumnName("paid_price").HasColumnType("decimal(18,2)");
                b.Property(p => p.currency).HasColumnName("currency").HasMaxLength(10);
                b.Property(p => p.fraud_status).HasColumnName("fraud_status").HasMaxLength(10);
                b.Property(p => p.transaction_id).HasColumnName("transaction_id").HasMaxLength(120);
                // E2b: IADE bu kimligi ister (paymentId DEGIL) - olculdu, bkz. Payment.item_transaction_id.
                b.Property(p => p.item_transaction_id).HasColumnName("item_transaction_id").HasMaxLength(120);
                b.Property(p => p.conversation_id).HasColumnName("conversation_id").HasMaxLength(120);
                b.Property(p => p.token).HasColumnName("token").HasMaxLength(120);
                b.Property(p => p.paid_at).HasColumnName("paid_at");
                b.Property(p => p.created_at).HasColumnName("created_at");
                b.HasIndex(p => p.conversation_id);
                b.HasIndex(p => p.token);
                b.HasIndex(p => p.order_id);
            });

            // Açıklayıcı yorum: Address tablosu
            modelBuilder.Entity<Address>(b =>
            {
                b.ToTable("addresses");
                b.HasKey(a => a.id);
                b.Property(a => a.customer_id).HasColumnName("customer_id");
                b.Property(a => a.title).HasColumnName("title").HasMaxLength(60);
                b.Property(a => a.full_name).HasColumnName("full_name").HasMaxLength(150);
                b.Property(a => a.phone).HasColumnName("phone").HasMaxLength(20);
                b.Property(a => a.city).HasColumnName("city").HasMaxLength(60);
                b.Property(a => a.district).HasColumnName("district").HasMaxLength(60);
                b.Property(a => a.full_address).HasColumnName("full_address").HasMaxLength(500);
                b.Property(a => a.zip_code).HasColumnName("zip_code").HasMaxLength(20);
                b.Property(a => a.is_default).HasColumnName("is_default");
                b.Property(a => a.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(a => a.created_at).HasColumnName("created_at");
                b.Property(a => a.updated_at).HasColumnName("updated_at");
                b.HasIndex(a => a.customer_id);
            });

            // Açıklayıcı yorum: WishlistItem tablosu
            modelBuilder.Entity<WishlistItem>(b =>
            {
                b.ToTable("wishlist_items");
                b.HasKey(w => w.id);
                b.Property(w => w.customer_id).HasColumnName("customer_id");
                b.Property(w => w.product_id).HasColumnName("product_id");
                b.Property(w => w.created_at).HasColumnName("created_at");
                // Aynı müşteri aynı ürünü tek kez favoriler
                b.HasIndex(w => new { w.customer_id, w.product_id }).IsUnique();
            });

            // Açıklayıcı yorum: AuditLog tablosu (denetim kaydı)
            modelBuilder.Entity<AuditLog>(b =>
            {
                b.ToTable("audit_logs");
                b.HasKey(a => a.id);
                b.Property(a => a.table_name).HasColumnName("table_name").HasMaxLength(100);
                b.Property(a => a.entity_id).HasColumnName("entity_id").HasMaxLength(60);
                b.Property(a => a.action).HasColumnName("action").HasMaxLength(20);
                b.Property(a => a.changes).HasColumnName("changes");
                b.Property(a => a.user_id).HasColumnName("user_id").HasMaxLength(60);
                b.Property(a => a.created_at).HasColumnName("created_at");
                b.HasIndex(a => new { a.table_name, a.created_at });
            });

            // Açıklayıcı yorum: ProductImage tablosu (çoklu ürün görseli)
            modelBuilder.Entity<ProductImage>(b =>
            {
                b.ToTable("product_images");
                b.HasKey(i => i.id);
                b.Property(i => i.product_id).HasColumnName("product_id");
                b.Property(i => i.image_url).HasColumnName("image_url").HasMaxLength(1000).IsRequired();
                b.Property(i => i.sort_order).HasColumnName("sort_order");
                b.Property(i => i.is_primary).HasColumnName("is_primary");
                b.Property(i => i.created_at).HasColumnName("created_at");
                b.HasIndex(i => i.product_id);
            });

            // Açıklayıcı yorum: "Stok gelince haber ver" talepleri
            modelBuilder.Entity<StockNotificationRequest>(b =>
            {
                b.ToTable("stock_notification_requests");
                b.HasKey(n => n.id);
                b.Property(n => n.product_id).HasColumnName("product_id");
                b.Property(n => n.size).HasColumnName("size").HasMaxLength(20);
                b.Property(n => n.email).HasColumnName("email").HasMaxLength(256).IsRequired();
                b.Property(n => n.is_notified).HasColumnName("is_notified");
                b.Property(n => n.created_at).HasColumnName("created_at");
                b.Property(n => n.notified_at).HasColumnName("notified_at");
                // SPRINT 8 MADDE 10 - abonelikten cikma jetonu. Aramanin TEK yolu bu oldugu icin
                // UNIQUE + indeksli; jeton uretimi cakisirsa insert gurultulu duser (sessizce
                // ikinci bir abonelik olusmaz).
                b.Property(n => n.unsubscribe_token).HasColumnName("unsubscribe_token").HasMaxLength(64).IsRequired();
                b.HasIndex(n => n.unsubscribe_token).IsUnique().HasDatabaseName("UX_stock_notification_requests_token");
                // Açıklayıcı yorum: NotifyBackInStock sorgusu (product_id + size + is_notified) için bileşik index
                b.HasIndex(n => new { n.product_id, n.size, n.is_notified });
            });

            // Açıklayıcı yorum: Sipariş durum geçmişi (zaman çizelgesi)
            modelBuilder.Entity<OrderStatusHistory>(b =>
            {
                b.ToTable("order_status_histories");
                b.HasKey(h => h.id);
                b.Property(h => h.order_id).HasColumnName("order_id");
                b.Property(h => h.status).HasColumnName("status");
                b.Property(h => h.note).HasColumnName("note").HasMaxLength(500);
                b.Property(h => h.created_at).HasColumnName("created_at");
                b.HasIndex(h => h.order_id); // timeline sorgusu
            });

            // Açıklayıcı yorum: Son görüntülenen ürünler
            modelBuilder.Entity<RecentlyViewedProduct>(b =>
            {
                b.ToTable("recently_viewed_products");
                b.HasKey(r => r.id);
                b.Property(r => r.customer_id).HasColumnName("customer_id");
                b.Property(r => r.product_id).HasColumnName("product_id");
                b.Property(r => r.viewed_at).HasColumnName("viewed_at");
                b.HasIndex(r => new { r.customer_id, r.product_id }).IsUnique(); // upsert (müşteri+ürün tek satır)
                b.HasIndex(r => new { r.customer_id, r.viewed_at });            // son N sorgusu
            });

            // Açıklayıcı yorum: StockReservation tablosu (rezervasyon - oversell + terk edilen sepet koruması)
            modelBuilder.Entity<StockReservation>(b =>
            {
                b.ToTable("stock_reservations");
                b.HasKey(r => r.id);
                b.Property(r => r.order_id).HasColumnName("order_id");
                b.Property(r => r.product_id).HasColumnName("product_id");
                b.Property(r => r.size).HasColumnName("size").HasMaxLength(20);
                b.Property(r => r.quantity).HasColumnName("quantity");
                b.Property(r => r.status).HasColumnName("status");
                b.Property(r => r.expires_at).HasColumnName("expires_at");
                b.Property(r => r.created_at).HasColumnName("created_at");
                b.Property(r => r.closed_at).HasColumnName("closed_at");
                b.HasIndex(r => r.order_id);
                b.HasIndex(r => new { r.status, r.expires_at }); // job sorgusu için
            });

            // Açıklayıcı yorum: Shipment tablosu (kargo takip)
            modelBuilder.Entity<Shipment>(b =>
            {
                b.ToTable("shipments");
                b.HasKey(s => s.id);
                b.Property(s => s.order_id).HasColumnName("order_id");
                b.Property(s => s.carrier).HasColumnName("carrier");
                b.Property(s => s.tracking_number).HasColumnName("tracking_number").HasMaxLength(100);
                b.Property(s => s.status).HasColumnName("status");
                b.Property(s => s.last_status_text).HasColumnName("last_status_text").HasMaxLength(300);
                b.Property(s => s.shipped_at).HasColumnName("shipped_at");
                b.Property(s => s.estimated_delivery).HasColumnName("estimated_delivery");
                b.Property(s => s.delivered_at).HasColumnName("delivered_at");
                b.Property(s => s.created_at).HasColumnName("created_at");
                b.Property(s => s.last_checked_at).HasColumnName("last_checked_at");
                b.HasIndex(s => s.order_id).IsUnique();
                b.HasIndex(s => s.tracking_number);
            });

            // Açıklayıcı yorum: CustomerDevice tablosu (push token)
            modelBuilder.Entity<CustomerDevice>(b =>
            {
                b.ToTable("customer_devices");
                b.HasKey(d => d.id);
                b.Property(d => d.customer_id).HasColumnName("customer_id");
                b.Property(d => d.device_token).HasColumnName("device_token").HasMaxLength(500);
                b.Property(d => d.platform).HasColumnName("platform");
                b.Property(d => d.is_active).HasColumnName("is_active");
                b.Property(d => d.created_at).HasColumnName("created_at");
                b.Property(d => d.last_used_at).HasColumnName("last_used_at");
                b.HasIndex(d => d.customer_id);
                b.HasIndex(d => d.device_token).IsUnique();
            });

            // Açıklayıcı yorum: Invoice tablosu (fatura)
            // Açıklayıcı yorum: FATURA KALEMLERİ - kalem bazlı KDV. Oran fatura anında DONDURULUR.
            modelBuilder.Entity<InvoiceItem>(b =>
            {
                b.ToTable("invoice_items");
                b.HasKey(i => i.id);
                b.Property(i => i.invoice_id).HasColumnName("invoice_id");
                b.Property(i => i.product_id).HasColumnName("product_id");
                b.Property(i => i.product_name).HasColumnName("product_name").HasMaxLength(200);
                b.Property(i => i.quantity).HasColumnName("quantity");
                b.Property(i => i.unit_price).HasColumnName("unit_price").HasColumnType("decimal(18,2)");
                b.Property(i => i.line_subtotal).HasColumnName("line_subtotal").HasColumnType("decimal(18,2)");
                b.Property(i => i.vat_rate).HasColumnName("vat_rate").HasColumnType("decimal(5,4)");
                b.Property(i => i.vat_amount).HasColumnName("vat_amount").HasColumnType("decimal(18,2)");
                b.Property(i => i.line_total).HasColumnName("line_total").HasColumnType("decimal(18,2)");
                b.Property(i => i.created_at).HasColumnName("created_at");
                // Fatura kalemleri her zaman fatura uzerinden okunur.
                b.HasIndex(i => i.invoice_id);
            });

            modelBuilder.Entity<Invoice>(b =>
            {
                b.ToTable("invoices");
                b.HasKey(i => i.id);
                b.Property(i => i.order_id).HasColumnName("order_id");
                b.Property(i => i.customer_id).HasColumnName("customer_id");
                b.Property(i => i.invoice_number).HasColumnName("invoice_number").HasMaxLength(40);
                b.Property(i => i.subtotal).HasColumnName("subtotal").HasColumnType("decimal(18,2)");
                b.Property(i => i.tax_amount).HasColumnName("tax_amount").HasColumnType("decimal(18,2)");
                b.Property(i => i.total).HasColumnName("total").HasColumnType("decimal(18,2)");
                b.Property(i => i.tax_rate).HasColumnName("tax_rate").HasColumnType("decimal(5,4)");
                b.Property(i => i.status).HasColumnName("status");
                b.Property(i => i.pdf_url).HasColumnName("pdf_url").HasMaxLength(300);
                b.Property(i => i.created_at).HasColumnName("created_at");
                b.HasIndex(i => i.customer_id);
                b.HasIndex(i => i.order_id).IsUnique();   // sipariş başına tek fatura
                b.HasIndex(i => i.invoice_number).IsUnique();
            });

            // Açıklayıcı yorum: ReturnRequest tablosu (iade/değişim)
            modelBuilder.Entity<ReturnRequest>(b =>
            {
                b.ToTable("return_requests");
                b.HasKey(r => r.id);
                b.Property(r => r.order_id).HasColumnName("order_id");
                b.Property(r => r.customer_id).HasColumnName("customer_id");
                b.Property(r => r.product_id).HasColumnName("product_id");
                b.Property(r => r.size).HasColumnName("size").HasMaxLength(20);
                b.Property(r => r.quantity).HasColumnName("quantity");
                b.Property(r => r.reason).HasColumnName("reason");
                b.Property(r => r.description).HasColumnName("description").HasMaxLength(1000);
                b.Property(r => r.return_type).HasColumnName("return_type");
                b.Property(r => r.status).HasColumnName("status");
                b.Property(r => r.refund_amount).HasColumnName("refund_amount").HasColumnType("decimal(18,2)");
                b.Property(r => r.refund_id).HasColumnName("refund_id").HasMaxLength(120);
                b.Property(r => r.admin_note).HasColumnName("admin_note").HasMaxLength(500);
                b.Property(r => r.created_at).HasColumnName("created_at");
                b.Property(r => r.processed_at).HasColumnName("processed_at");
                b.HasIndex(r => r.customer_id);
                b.HasIndex(r => r.order_id);
                b.HasIndex(r => r.status);
            });

            // Açıklayıcı yorum: SecurityEvent tablosu (güvenlik olayları - SIEM/alerting)
            modelBuilder.Entity<SecurityEvent>(b =>
            {
                b.ToTable("security_events");
                b.HasKey(e => e.id);
                b.Property(e => e.event_type).HasColumnName("event_type").HasMaxLength(60);
                b.Property(e => e.severity).HasColumnName("severity").HasMaxLength(20);
                b.Property(e => e.customer_id).HasColumnName("customer_id");
                b.Property(e => e.ip_address).HasColumnName("ip_address").HasMaxLength(60);
                b.Property(e => e.user_agent).HasColumnName("user_agent").HasMaxLength(300);
                b.Property(e => e.detail).HasColumnName("detail").HasMaxLength(1000);
                b.Property(e => e.created_at).HasColumnName("created_at");
                b.HasIndex(e => new { e.event_type, e.created_at });
                b.HasIndex(e => e.customer_id);
            });

            // ── B14: Ek performans index'leri ──
            // Açıklayıcı yorum: Müşteri siparişleri sıralı sorgu
            modelBuilder.Entity<Order>().HasIndex(o => new { o.customer_id, o.created_at });
            // Açıklayıcı yorum: Ürünün onaylı yorumları sorgusu
            modelBuilder.Entity<ProductReview>().HasIndex(r => new { r.product_id, r.review_status });

            // ── Global query filter: soft-delete'li entity'lerde pasif kayıtlar TÜM sorgularda otomatik gizlenir ──
            // Açıklayıcı yorum: is_active=false kayıtlar varsayılan sorgulara gelmez (manuel filtre unutma riski biter).
            // Admin'in silinenleri görmesi gerekirse .IgnoreQueryFilters() kullanılır.
            modelBuilder.Entity<Product>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<Category>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<Coupon>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<Collection>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<Customer>().HasQueryFilter(e => e.is_active);
            // Prevention: referral_code FILTERED-UNIQUE (nullable -> NOT NULL) - eszamanli kod uretiminde cakisma engeli.
            modelBuilder.Entity<Customer>().HasIndex(c => c.referral_code).IsUnique().HasFilter("[referral_code] IS NOT NULL");
            // Prevention: bekleyen (is_notified=0) abonelik (product,size,email) FILTERED-UNIQUE - cift abonelik/spam engeli.
            modelBuilder.Entity<StockNotificationRequest>().HasIndex(n => new { n.product_id, n.size, n.email }).IsUnique().HasFilter("[is_notified] = 0");
            // Prevention: bekleyen fiyat-dusme abonelik (product,email) FILTERED-UNIQUE - cift abonelik engeli.
            modelBuilder.Entity<PriceDropSubscription>().HasIndex(p => new { p.product_id, p.email }).IsUnique().HasFilter("[is_notified] = 0");
            modelBuilder.Entity<ProductReview>().HasIndex(r => new { r.customer_id, r.product_id }).IsUnique().HasFilter("[is_active] = 1");
            modelBuilder.Entity<Address>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<Cart>().HasQueryFilter(e => e.is_active);
            // ═══ B1: Soft-delete global filtre genisletme ══════════════════════════════════
            //
            // FAZ 0 / K4 - DISLAMA LISTESI 6'YA TAMAMLANDI (ONCE 4 YAZIYORDU).
            // Yorum "ProductStock/UserSession/CustomerDevice/GiftCard" diyordu; olculdu ki
            // is_active TASIYAN ama query filter'i OLMAYAN entity sayisi ALTI - Seller ve
            // ProductQuestion listede EKSIKTI. Kod DEGISMEDI (olculen guvenlik/veri boslugu
            // SIFIR: altisinda da pasif satir kritik yuzeylerden gecemiyor); duzelen sey
            // BELGE BORCU. Her biri icin gerekce - iki tanesinde filtre eklemek AKTIF ZARAR:
            //
            //  GiftCard        : is_active IKI anlam tasir - soft-delete VE "tuketildi"
            //                    (EfGiftCardDal.TryRedeemAsync redeem'de false set eder).
            //                    Bakiye+redeem okumalarinin IKISI DE zaten `&& g.is_active`.
            //                    Filtre eklemek tuketilmis karti DENETIMDEN de gizlerdi.
            //  ProductStock    : dokuz okuma yerinin dokuzunda da `&& s.is_active` var
            //                    (StockManager, ProductManager, SearchManager, EfProductStockDal,
            //                    EfProductDal). Capraz not yukarida, ProductStock blogunda.
            //  UserSession     : *** FILTRE EKLEMEK IKI SEYI BIRDEN BOZAR ***
            //                    (a) G1 - GetByRefreshTokenAnyStateAsync filtreyi BILEREK
            //                        kaldiriyor ki DONDURULMUS jetonun yeniden sunulmasi
            //                        (refresh token hirsizligi sinyali) tespit edilebilsin;
            //                    (b) DataRetentionJob `!s.is_active && created_at < -90g`
            //                        satirlarini siler - filtre eklenirse HICBIR SATIR GOREMEZ.
            //  CustomerDevice  : *** FILTRE EKLEMEK UNIQUE IHLALI URETIR ***
            //                    RegisterDevice pasif cihazi FILTRESIZ okuyup YENIDEN
            //                    AKTIFLESTIRIYOR; filtre eklenirse satir gorunmez, kod YENI
            //                    satir INSERT eder ve device_token UNIQUE indeksi patlar.
            //                    Push gonderimi zaten `&& d.is_active` - pasife push GITMEZ.
            //  Seller          : her okuma yeri korumali - login `!seller.is_active` -> 403,
            //                    uc panel ucunun ucunde de ayni kontrol. Filtre eklense
            //                    davranis 403 -> 404'e kayardi (null kontrolu var, cokmez).
            //  ProductQuestion : bayrak YAZ-BIR-KEZ - depoda `is_active = false` yapan HICBIR
            //                    kod yolu yok, yalniz olusturmada `true`. Pasif soru bugun
            //                    OLUSAMAZ; dort okumanin dordu de zaten filtreli. Asimetri
            //                    pratikte BOS. (Bayragin fiilen kullanilmadigi deftere yazildi.)
            modelBuilder.Entity<SubCategory>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<ProductAttribute>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<ProductReview>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<CartItem>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<CollectionItem>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<Content>().HasQueryFilter(e => e.is_active);
            modelBuilder.Entity<SizeGuideEntry>().HasQueryFilter(e => e.is_active);

            // === Dalga 2-3: Sadakat + Kredi + Hediye kartı ===
            modelBuilder.Entity<LoyaltyTransaction>(b =>
            {
                b.ToTable("loyalty_transactions");
                b.HasKey(t => t.id);
                b.Property(t => t.customer_id).HasColumnName("customer_id");
                b.Property(t => t.points).HasColumnName("points");
                b.Property(t => t.type).HasColumnName("type");
                b.Property(t => t.reason).HasColumnName("reason").HasMaxLength(200);
                b.Property(t => t.order_id).HasColumnName("order_id");
                b.Property(t => t.created_at).HasColumnName("created_at");
                b.HasIndex(t => new { t.customer_id, t.created_at });
                // SIPARIS BASINA TEK KAZANIM (filtreli UNIQUE): bir siparis icin yalnizca BIR Earn
                // satiri olabilir. Uygulama katmaninda atomik durum gecisi (payments Pending->Success)
                // zaten tek kazanan biraktigi icin bu indeks ikinci savunma hattidir - "asagi katman
                // zaten emiyor" varsayimina GUVENILMEZ, kural veritabaninda da durur.
                // Filtre: yalniz Earn (type=0) ve order_id NOT NULL. Redeem (geri alim) ayni order_id
                // ile yazilabilir; siparissiz manuel kazanimlar (order_id NULL) sinirlanmaz.
                b.HasIndex(t => t.order_id)
                    .IsUnique()
                    .HasFilter("[order_id] IS NOT NULL AND [type] = 0")
                    .HasDatabaseName("UX_loyalty_transactions_order_earn");
            });
            modelBuilder.Entity<StoreCreditTransaction>(b =>
            {
                b.ToTable("store_credit_transactions");
                b.HasKey(t => t.id);
                b.Property(t => t.customer_id).HasColumnName("customer_id");
                b.Property(t => t.amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
                b.Property(t => t.type).HasColumnName("type");
                b.Property(t => t.reason).HasColumnName("reason").HasMaxLength(200);
                b.Property(t => t.order_id).HasColumnName("order_id");
                b.Property(t => t.created_at).HasColumnName("created_at");
                b.HasIndex(t => new { t.customer_id, t.created_at });

                // SPRINT 8 MADDE 3 - REFERANS ODULU IDEMPOTENCY'SI DB DUZEYINE INDI.
                // Yan etkiler outbox'a tasindi ve at-least-once oldu. Dort adimin ucunun
                // idempotentlik dayanagi zaten VERITABANINDAYDI (fatura: "zaten var" kontrolu,
                // sadakat: UX_loyalty_transactions_order_earn, kupon: turetme + UNIQUE).
                // Referans odulunun tek korumasi UYGULAMA KATMANINDAKI oku-sonra-davran guard'iydi:
                // "bu musteriye daha once davet-edilen odulu verildi mi?" Eszamanli iki teslimat
                // ikisi de "verilmemis" okuyup IKI KEZ odeyebilirdi. Kisit DB'ye indirildi.
                //
                // FILTRELI: yalniz DAVET EDILEN odulu tekil. "Davet EDEN" odulu TEKRARLANABILIR -
                // bir kullanici birden fazla kisiyi davet edebilir ve her biri icin odul alir.
                // Filtresiz bir (customer_id, reason) kisiti o mesru davranisi KIRARDI.
                //
                // DIKKAT - DIZGE BAGIMLILIGI: filtre, ReferralManager'daki sebep metnine BIREBIR
                // baglidir (RefereeRewardReason sabiti). Metin degisirse indeks eslesmez ve koruma
                // SESSIZCE kalkar; ikisi BIRLIKTE degistirilmeli.
                b.HasIndex(t => t.customer_id)
                    .IsUnique()
                    .HasFilter("[reason] = N'Referans ödülü (davet edilen)'")
                    .HasDatabaseName("UX_store_credit_referee_reward");
            });
            modelBuilder.Entity<ConsentRecord>(b =>
            {
                b.ToTable("consent_records");
                b.HasKey(c => c.id);
                b.HasIndex(c => new { c.customer_id, c.consent_type });
                b.Property(c => c.consent_type).HasMaxLength(40);
                b.Property(c => c.document_version).HasMaxLength(40);
            });

            modelBuilder.Entity<GiftCard>(b =>
            {
                b.ToTable("gift_cards");
                b.HasKey(g => g.id);
                b.Property(g => g.code).HasColumnName("code").HasMaxLength(20).IsRequired();
                b.Property(g => g.initial_amount).HasColumnName("initial_amount").HasColumnType("decimal(18,2)");
                b.Property(g => g.balance).HasColumnName("balance").HasColumnType("decimal(18,2)");
                b.Property(g => g.is_active).HasColumnName("is_active");
                b.Property(g => g.redeemed_by).HasColumnName("redeemed_by");
                b.Property(g => g.created_at).HasColumnName("created_at");
                b.Property(g => g.redeemed_at).HasColumnName("redeemed_at");
                b.HasIndex(g => g.code).IsUnique();
            });

            // === Dalga 4: Fiyat düşüş bildirimi ===
            modelBuilder.Entity<PriceDropSubscription>(b =>
            {
                b.ToTable("price_drop_subscriptions");
                b.HasKey(p => p.id);
                b.Property(p => p.product_id).HasColumnName("product_id");
                b.Property(p => p.email).HasColumnName("email").HasMaxLength(256).IsRequired();
                b.Property(p => p.subscribed_price).HasColumnName("subscribed_price").HasColumnType("decimal(18,2)");
                b.Property(p => p.is_notified).HasColumnName("is_notified");
                b.Property(p => p.created_at).HasColumnName("created_at");
                b.Property(p => p.notified_at).HasColumnName("notified_at");
                // SPRINT 8 MADDE 10 - abonelikten cikma jetonu (stok bildirimiyle ayni gerekce).
                b.Property(p => p.unsubscribe_token).HasColumnName("unsubscribe_token").HasMaxLength(64).IsRequired();
                b.HasIndex(p => p.unsubscribe_token).IsUnique().HasDatabaseName("UX_price_drop_subscriptions_token");
                b.HasIndex(p => new { p.product_id, p.is_notified });
            });

            // === Dalga 5: Yorum güçlendirme ===
            modelBuilder.Entity<ReviewHelpfulVote>(b =>
            {
                b.ToTable("review_helpful_votes");
                b.HasKey(v => v.id);
                b.Property(v => v.review_id).HasColumnName("review_id");
                b.Property(v => v.customer_id).HasColumnName("customer_id");
                b.Property(v => v.created_at).HasColumnName("created_at");
                b.HasIndex(v => new { v.review_id, v.customer_id }).IsUnique();
            });
            modelBuilder.Entity<ProductQuestion>(b =>
            {
                b.ToTable("product_questions");
                b.HasKey(q => q.id);
                b.Property(q => q.product_id).HasColumnName("product_id");
                b.Property(q => q.customer_id).HasColumnName("customer_id");
                b.Property(q => q.question).HasColumnName("question").HasMaxLength(1000).IsRequired();
                b.Property(q => q.answer).HasColumnName("answer").HasMaxLength(2000);
                b.Property(q => q.created_at).HasColumnName("created_at");
                b.Property(q => q.answered_at).HasColumnName("answered_at");
                b.HasIndex(q => new { q.product_id, q.is_answered });
            });

            // === Dalga 11: Ürün özellikleri (faceted search) ===
            modelBuilder.Entity<ProductAttribute>(b =>
            {
                b.ToTable("product_attributes");
                b.HasKey(a => a.id);
                b.Property(a => a.product_id).HasColumnName("product_id");
                b.Property(a => a.attribute_key).HasColumnName("attribute_key").HasMaxLength(50).IsRequired();
                b.Property(a => a.attribute_value).HasColumnName("attribute_value").HasMaxLength(100).IsRequired();
                b.Property(a => a.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(a => a.created_at).HasColumnName("created_at");
                b.HasIndex(a => new { a.attribute_key, a.attribute_value });
                b.HasIndex(a => a.product_id);
            });

            // === Dalga 12: Beden rehberi ===
            modelBuilder.Entity<SizeGuideEntry>(b =>
            {
                b.ToTable("size_guide_entries");
                b.HasKey(e => e.id);
                b.Property(e => e.category_id).HasColumnName("category_id");
                b.Property(e => e.size_label).HasColumnName("size_label").HasMaxLength(20).IsRequired();
                b.Property(e => e.bust_cm).HasColumnName("bust_cm").HasColumnType("decimal(6,2)");
                b.Property(e => e.waist_cm).HasColumnName("waist_cm").HasColumnType("decimal(6,2)");
                b.Property(e => e.hip_cm).HasColumnName("hip_cm").HasColumnType("decimal(6,2)");
                b.Property(e => e.length_cm).HasColumnName("length_cm").HasColumnType("decimal(6,2)");
                b.Property(e => e.is_active).HasColumnName("is_active").HasDefaultValue(true);
                b.Property(e => e.sort_order).HasColumnName("sort_order");
                b.Property(e => e.created_at).HasColumnName("created_at");
                b.HasIndex(e => e.category_id);
            });

            // === user_type varsayilan: DB seviyesinde Customer(2) - mevcut satirlar/yeni kayitlar guvenli varsayilan ===
            // NOT: Mevcut Customer satirlari icin migration user_type=2 backfill etmeli; admin(1) satirlari elle atanir.
            modelBuilder.Entity<Customer>().Property(c => c.user_type).HasDefaultValue((byte)2);

            // ══ REFERANS BUTUNLUGU - TEK MERKEZ (D-SEMA-FIX) ══════════════════════════════════
            //
            // Entity'ler DUZ (navigation YOK), bu yuzden navigation'siz Fluent API kullaniliyor.
            // Silme davranisi HER FK'da Restrict = SQL Server'da ON DELETE NO ACTION.
            // CASCADE REDDEDILDI (olculdu): uretimde silme SOFT'tur (is_active=false) ve fiziksel
            // silme yapan TEK yol dogrudan SQL'dir; cascade tam da durdurulmasi gereken anda
            // gecmisi SESSIZCE goturur.
            //
            // AD BICIMI: FK_<cocuk_tablo>_<kolon> (KISA bicim). EF'in varsayilani
            // FK_<cocuk>_<ebeveyn>_<kolon> (UZUN bicim) idi; ikisi ayni kolonda MUKERRER kisit
            // uretir. Depo tarihinde bu iki bicim yan yana yasadi - D-SEMA olcumu ortak dokuz
            // iliskinin SEKIZINDE ad ayrismasi buldu. Bu blok ayrismayi kapatir; asagidaki
            // sekiz satirdaki HasConstraintName BILINCLI BIR YENIDEN ADLANDIRMADIR.
            //
            // KAPSAM NEREDEN GELDI (D-SEMA, olcum): eski database/mssql/01_schema.sql 55 FK
            // beyan ediyordu ama dokumandaki komutla yalniz 17'si kuruluyordu (batch abort).
            // 54 gecerli adayin tamami GERCEK dev verisine karsi tarandi.
            //
            // TASINMAYAN IKI ADAY (gerekce):
            //   * orders.payment_id -> payments.id : ANLAMSIZ. Order.payment_id bir string?'tir
            //     ve IYZICO'NUN PaymentId'sini tasir (IyzicoPaymentManager), bizim payments
            //     tablomuza isaret ETMEZ. Tip bile uyumsuz (nvarchar -> int) ve sema dosyasinda
            //     bu satir batch'i dusuruyordu. Kaynak: eski generate_schema.py FK'lari
            //     ADLANDIRMA KURALINDAN cikariyordu ("<x>_id -> <x>s(id)"), modelden degil.
            //   * consent_records.customer_id -> customers.id : KULLANICI KARARI, FK KONMAZ.
            //     KVKK'da riza kaydi, hesap silindikten sonra da "su kisi su tarihte suna riza
            //     verdi" kaniti olarak saklanmasi GEREKEBILIR; FK bunu imkansiz kilardi.
            //     Dev'deki 6 yetim satir SILINMEZ. Ayrinti: CLAUDE.md D-SEMA-FIX bolumu.
            //
            // NOT: product_stocks -> products FK'si (Dalga D / D2) bu bloga TASINMADI, kendi
            // yapilandirma blogunda duruyor (yukarida, ProductStock). Adi zaten kisa bicimde.

            // ── (1) MEVCUT DOKUZ ILISKI - ad KISA bicime cekiliyor (yeniden adlandirma) ──────
            modelBuilder.Entity<Order>().HasOne<Customer>().WithMany().HasForeignKey(o => o.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_orders_customer_id");
            modelBuilder.Entity<OrderItem>().HasOne<Order>().WithMany().HasForeignKey(i => i.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_items_order_id");
            modelBuilder.Entity<OrderItem>().HasOne<Product>().WithMany().HasForeignKey(i => i.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_items_product_id");
            modelBuilder.Entity<Address>().HasOne<Customer>().WithMany().HasForeignKey(a => a.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_addresses_customer_id");
            modelBuilder.Entity<Cart>().HasOne<Customer>().WithMany().HasForeignKey(c => c.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_carts_customer_id");
            modelBuilder.Entity<CartItem>().HasOne<Cart>().WithMany().HasForeignKey(i => i.cart_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_cart_items_cart_id");
            modelBuilder.Entity<ProductReview>().HasOne<Product>().WithMany().HasForeignKey(r => r.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_product_reviews_product_id");
            modelBuilder.Entity<WishlistItem>().HasOne<Customer>().WithMany().HasForeignKey(w => w.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_wishlist_items_customer_id");

            // ── (2) VERI KANITI OLAN 28 ILISKI ──────────────────────────────────────────────
            // Her biri GERCEK dev verisinde ihlalsiz olcculdu (cocuk tablo DOLU: 127'ye kadar satir).
            modelBuilder.Entity<CartItem>().HasOne<Product>().WithMany().HasForeignKey(i => i.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_cart_items_product_id");
            modelBuilder.Entity<Invoice>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_invoices_customer_id");
            modelBuilder.Entity<Invoice>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_invoices_order_id");
            modelBuilder.Entity<LoyaltyTransaction>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_loyalty_transactions_customer_id");
            modelBuilder.Entity<LoyaltyTransaction>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_loyalty_transactions_order_id");
            modelBuilder.Entity<OrderSnapshotItem>().HasOne<OrderSnapshot>().WithMany().HasForeignKey(e => e.order_snapshot_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_snapshot_items_order_snapshot_id");
            modelBuilder.Entity<OrderSnapshotItem>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_snapshot_items_product_id");
            modelBuilder.Entity<OrderSnapshot>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_snapshots_customer_id");
            modelBuilder.Entity<OrderSnapshot>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_snapshots_order_id");
            modelBuilder.Entity<OrderStatusHistory>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_order_status_histories_order_id");
            modelBuilder.Entity<Order>().HasOne<Address>().WithMany().HasForeignKey(e => e.address_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_orders_address_id");
            modelBuilder.Entity<Payment>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_payments_order_id");
            modelBuilder.Entity<PriceDropSubscription>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_price_drop_subscriptions_product_id");
            modelBuilder.Entity<ProductImage>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_product_images_product_id");
            modelBuilder.Entity<Product>().HasOne<Category>().WithMany().HasForeignKey(e => e.category_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_products_category_id");
            modelBuilder.Entity<Product>().HasOne<SubCategory>().WithMany().HasForeignKey(e => e.sub_category_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_products_sub_category_id");
            modelBuilder.Entity<ReturnRequest>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_return_requests_customer_id");
            modelBuilder.Entity<ReturnRequest>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_return_requests_order_id");
            modelBuilder.Entity<ReturnRequest>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_return_requests_product_id");
            modelBuilder.Entity<SecurityEvent>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_security_events_customer_id");
            modelBuilder.Entity<Shipment>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_shipments_order_id");
            modelBuilder.Entity<StockMovement>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_stock_movements_product_id");
            modelBuilder.Entity<StockNotificationRequest>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_stock_notification_requests_product_id");
            modelBuilder.Entity<StockReservation>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_stock_reservations_order_id");
            modelBuilder.Entity<StockReservation>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_stock_reservations_product_id");
            modelBuilder.Entity<StoreCreditTransaction>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_store_credit_transactions_customer_id");
            modelBuilder.Entity<StoreCreditTransaction>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_store_credit_transactions_order_id");
            modelBuilder.Entity<UserSession>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_user_sessions_customer_id");

            // ── (3) VERI KANITI OLMAYAN 16 ILISKI - cocuk tablo dev'de BOS ──────────────────
            // DURUST KAYIT: bunlarin dogrulugu VERIDEN gelmiyor, YAZMA YOLU OKUNARAK dogrulandi.
            // Her birinin tek yazicisi bir manager'dir ve kimligi token'dan/dogrulanmis bir
            // DTO'dan alir; sentinel (0) ya da dis sistem referansi kullanan YOK.
            // Tip uyumu ayrica olculdu (hepsi int -> int).
            modelBuilder.Entity<CollectionItem>().HasOne<Collection>().WithMany().HasForeignKey(e => e.collection_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_collection_items_collection_id");
            modelBuilder.Entity<CollectionItem>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_collection_items_product_id");
            modelBuilder.Entity<CouponUsage>().HasOne<Coupon>().WithMany().HasForeignKey(e => e.coupon_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_coupon_usages_coupon_id");
            modelBuilder.Entity<CouponUsage>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_coupon_usages_customer_id");
            modelBuilder.Entity<CouponUsage>().HasOne<Order>().WithMany().HasForeignKey(e => e.order_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_coupon_usages_order_id");
            modelBuilder.Entity<CustomerDevice>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_customer_devices_customer_id");
            modelBuilder.Entity<ProductAttribute>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_product_attributes_product_id");
            modelBuilder.Entity<ProductQuestion>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_product_questions_customer_id");
            modelBuilder.Entity<ProductQuestion>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_product_questions_product_id");
            modelBuilder.Entity<ProductReview>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_product_reviews_customer_id");
            modelBuilder.Entity<RecentlyViewedProduct>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_recently_viewed_products_customer_id");
            modelBuilder.Entity<RecentlyViewedProduct>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_recently_viewed_products_product_id");
            modelBuilder.Entity<ReviewHelpfulVote>().HasOne<Customer>().WithMany().HasForeignKey(e => e.customer_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_review_helpful_votes_customer_id");
            modelBuilder.Entity<SizeGuideEntry>().HasOne<Category>().WithMany().HasForeignKey(e => e.category_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_size_guide_entries_category_id");
            modelBuilder.Entity<SubCategory>().HasOne<Category>().WithMany().HasForeignKey(e => e.category_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_sub_categories_category_id");
            modelBuilder.Entity<WishlistItem>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_wishlist_items_product_id");

            // ── (4) ESKI SEMA DOSYASININ HIC TANIMLAMADIGI UC GERCEK ILISKI ────────────────
            // D-SEMA olcumu, dosyanin yalniz FAZLA FK tanimlamadigini; GERCEK olan bazilarini
            // da ATLADIGINI gosterdi. Kok sebep yine ureteç: FK'lari "<x>_id -> <x>s(id)"
            // kuralindan cikariyordu, yani `review_id` icin olmayan bir `reviews` tablosunu
            // ariyor ve BULAMAYINCA SESSIZCE atliyordu. `invoice_items` ise hic kapsanmamisti.
            // Kullanici karari: ucu de EKLENSIN.
            //   invoice_items.invoice_id -> invoices.id : fatura kalemi FATURASIZ olamaz.
            //     Yazma yolu okundu: InvoiceManager once faturayi yazar (satir 168), SONRA
            //     `ii.invoice_id = invoice.id` atar (172) - id her zaman GERCEK.
            //     VERI KANITI VAR: 27 satir, yetim 0.
            //   invoice_items.product_id -> products.id : kalem `order_items.product_id`den
            //     gelir. VERI KANITI VAR: 27 satir, yetim 0.
            //   review_helpful_votes.review_id -> product_reviews.id : ProductReviewManager
            //     .VoteHelpful yorumu ONCE arar ve yoksa 404 doner (satir 138), yani id
            //     dogrulanmis gelir. Tablo dev'de BOS - kanit YAZMA YOLUNDAN.
            // NOT: products.seller_id / order_items.seller_id BILEREK EKLENMEDI - satici
            // modulu kapali (Seller:RegistrationEnabled=false, sellers 0 satir) ve iki FK
            // modul acilirken G4 on kosuluyla BIRLIKTE eklenecek (bkz. KARARLAR).
            modelBuilder.Entity<InvoiceItem>().HasOne<Invoice>().WithMany().HasForeignKey(e => e.invoice_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_invoice_items_invoice_id");
            modelBuilder.Entity<InvoiceItem>().HasOne<Product>().WithMany().HasForeignKey(e => e.product_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_invoice_items_product_id");
            modelBuilder.Entity<ReviewHelpfulVote>().HasOne<ProductReview>().WithMany().HasForeignKey(e => e.review_id).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_review_helpful_votes_review_id");

            base.OnModelCreating(modelBuilder);
        }
    }
}
