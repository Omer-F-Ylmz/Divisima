using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class ReferansButunluguTekMerkez : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === D-SEMA-FIX: REFERANS BUTUNLUGU TEK MERKEZE INDI ============================
            //
            // NEREDEN GELDI (olcum, tahmin degil): eski `database/mssql/01_schema.sql` 55 FK
            // BEYAN ediyordu ama dokumandaki komutla (sqlcmd -i, -b YOK, dosyada GO YOK) yalniz
            // 17'si kuruluyordu: satir 635'teki FK_orders_payment_id tip uyumsuzlugundan patlayip
            // BATCH'I DUSURUYOR, sonraki 37 FK ve 65 indeks HIC olusmuyordu - ve sqlcmd EXIT 0
            // donuyordu. Yani "55 FK" kagit uzerinde bir iddiaydi.
            //
            // KULLANICI KARARI: tek dogruluk kaynagi EF migrations. Bu migration, dogrulanmis
            // iliskileri KAGITTAN UYGULANAN KORUMAYA cevirir.
            //
            // KAPSAM (54 gecerli adayin tamami GERCEK dev verisine karsi tarandi):
            //    9  zaten EF'te vardi -> yalnizca ADI kisa bicime cekiliyor (asagidaki 8 + D2'nin
            //       product_stocks FK'si; sonuncusu zaten kisa bicimdeydi)
            //   28  veri kaniti VAR (cocuk tablo dolu, ihlal 0 - 127 satira kadar)
            //   16  veri kaniti YOK (cocuk tablo bos) -> YAZMA YOLU OKUNARAK dogrulandi:
            //       hepsinin tek yazicisi bir manager ve kimligi token'dan / dogrulanmis DTO'dan
            //       aliyor; sentinel (0) ya da dis sistem referansi kullanan YOK
            //    1  orders.payment_id: TASINMADI - ANLAMSIZ (Iyzico'nun PaymentId'si, bizim
            //       payments tablomuz DEGIL; tip de uyumsuz)
            //    1  consent_records.customer_id: TASINMADI - KULLANICI KARARI (KVKK riza kaydi
            //       hesap silindikten sonra da kanit olarak saklanmasi gerekebilir)
            //
            // ON KONTROL - SPRINT 6 KALIBI: kirli veri varsa HICBIR SATIR SILINMEDEN gurultulu
            // duser. Yetim satiri silmek, hala ona isaret eden bir gecmisi sessizce yok etmek
            // olurdu; hangi kaydin dogru oldugu karari OPERATORUNDUR.
            migrationBuilder.Sql(@"
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
");
            migrationBuilder.DropForeignKey(
                name: "FK_addresses_customers_customer_id",
                table: "addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_carts_cart_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_carts_customers_customer_id",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_order_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_products_product_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_customer_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_product_reviews_products_product_id",
                table: "product_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_wishlist_items_customers_customer_id",
                table: "wishlist_items");

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_product_id",
                table: "wishlist_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_store_credit_transactions_order_id",
                table: "store_credit_transactions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_product_id",
                table: "stock_reservations",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_helpful_votes_customer_id",
                table: "review_helpful_votes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_product_id",
                table: "return_requests",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_recently_viewed_products_product_id",
                table: "recently_viewed_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sub_category_id",
                table: "products",
                column: "sub_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_questions_customer_id",
                table: "product_questions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_address_id",
                table: "orders",
                column: "address_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_snapshots_customer_id",
                table: "order_snapshots",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_snapshots_order_id",
                table: "order_snapshots",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_snapshot_items_order_snapshot_id",
                table: "order_snapshot_items",
                column: "order_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_snapshot_items_product_id",
                table: "order_snapshot_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_usages_customer_id",
                table: "coupon_usages",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_usages_order_id",
                table: "coupon_usages",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_items_product_id",
                table: "collection_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_product_id",
                table: "cart_items",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "FK_addresses_customer_id",
                table: "addresses",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_cart_id",
                table: "cart_items",
                column: "cart_id",
                principalTable: "carts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_product_id",
                table: "cart_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_carts_customer_id",
                table: "carts",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_collection_items_collection_id",
                table: "collection_items",
                column: "collection_id",
                principalTable: "collections",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_collection_items_product_id",
                table: "collection_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_coupon_usages_coupon_id",
                table: "coupon_usages",
                column: "coupon_id",
                principalTable: "coupons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_coupon_usages_customer_id",
                table: "coupon_usages",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_coupon_usages_order_id",
                table: "coupon_usages",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_customer_devices_customer_id",
                table: "customer_devices",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_customer_id",
                table: "invoices",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_order_id",
                table: "invoices",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_loyalty_transactions_customer_id",
                table: "loyalty_transactions",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_loyalty_transactions_order_id",
                table: "loyalty_transactions",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_order_id",
                table: "order_items",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_product_id",
                table: "order_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_snapshot_items_order_snapshot_id",
                table: "order_snapshot_items",
                column: "order_snapshot_id",
                principalTable: "order_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_snapshot_items_product_id",
                table: "order_snapshot_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_snapshots_customer_id",
                table: "order_snapshots",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_snapshots_order_id",
                table: "order_snapshots",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_status_histories_order_id",
                table: "order_status_histories",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_address_id",
                table: "orders",
                column: "address_id",
                principalTable: "addresses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customer_id",
                table: "orders",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_order_id",
                table: "payments",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_price_drop_subscriptions_product_id",
                table: "price_drop_subscriptions",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_attributes_product_id",
                table: "product_attributes",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_images_product_id",
                table: "product_images",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_questions_customer_id",
                table: "product_questions",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_questions_product_id",
                table: "product_questions",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_reviews_customer_id",
                table: "product_reviews",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_reviews_product_id",
                table: "product_reviews",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_sub_category_id",
                table: "products",
                column: "sub_category_id",
                principalTable: "sub_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recently_viewed_products_customer_id",
                table: "recently_viewed_products",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recently_viewed_products_product_id",
                table: "recently_viewed_products",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_requests_customer_id",
                table: "return_requests",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_requests_order_id",
                table: "return_requests",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_requests_product_id",
                table: "return_requests",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_review_helpful_votes_customer_id",
                table: "review_helpful_votes",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_security_events_customer_id",
                table: "security_events",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipments_order_id",
                table: "shipments",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_size_guide_entries_category_id",
                table: "size_guide_entries",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_product_id",
                table: "stock_movements",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_notification_requests_product_id",
                table: "stock_notification_requests",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reservations_order_id",
                table: "stock_reservations",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_reservations_product_id",
                table: "stock_reservations",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_store_credit_transactions_customer_id",
                table: "store_credit_transactions",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_store_credit_transactions_order_id",
                table: "store_credit_transactions",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sub_categories_category_id",
                table: "sub_categories",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_sessions_customer_id",
                table: "user_sessions",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_wishlist_items_customer_id",
                table: "wishlist_items",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_wishlist_items_product_id",
                table: "wishlist_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_addresses_customer_id",
                table: "addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_cart_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_product_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_carts_customer_id",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "FK_collection_items_collection_id",
                table: "collection_items");

            migrationBuilder.DropForeignKey(
                name: "FK_collection_items_product_id",
                table: "collection_items");

            migrationBuilder.DropForeignKey(
                name: "FK_coupon_usages_coupon_id",
                table: "coupon_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_coupon_usages_customer_id",
                table: "coupon_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_coupon_usages_order_id",
                table: "coupon_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_customer_devices_customer_id",
                table: "customer_devices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_customer_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_order_id",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_loyalty_transactions_customer_id",
                table: "loyalty_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_loyalty_transactions_order_id",
                table: "loyalty_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_order_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_product_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_snapshot_items_order_snapshot_id",
                table: "order_snapshot_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_snapshot_items_product_id",
                table: "order_snapshot_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_snapshots_customer_id",
                table: "order_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_order_snapshots_order_id",
                table: "order_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_order_status_histories_order_id",
                table: "order_status_histories");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_address_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_customer_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_order_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_price_drop_subscriptions_product_id",
                table: "price_drop_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_product_attributes_product_id",
                table: "product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_images_product_id",
                table: "product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_product_questions_customer_id",
                table: "product_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_product_questions_product_id",
                table: "product_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_product_reviews_customer_id",
                table: "product_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_product_reviews_product_id",
                table: "product_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_products_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_sub_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_recently_viewed_products_customer_id",
                table: "recently_viewed_products");

            migrationBuilder.DropForeignKey(
                name: "FK_recently_viewed_products_product_id",
                table: "recently_viewed_products");

            migrationBuilder.DropForeignKey(
                name: "FK_return_requests_customer_id",
                table: "return_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_return_requests_order_id",
                table: "return_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_return_requests_product_id",
                table: "return_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_review_helpful_votes_customer_id",
                table: "review_helpful_votes");

            migrationBuilder.DropForeignKey(
                name: "FK_security_events_customer_id",
                table: "security_events");

            migrationBuilder.DropForeignKey(
                name: "FK_shipments_order_id",
                table: "shipments");

            migrationBuilder.DropForeignKey(
                name: "FK_size_guide_entries_category_id",
                table: "size_guide_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_product_id",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_notification_requests_product_id",
                table: "stock_notification_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_reservations_order_id",
                table: "stock_reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_reservations_product_id",
                table: "stock_reservations");

            migrationBuilder.DropForeignKey(
                name: "FK_store_credit_transactions_customer_id",
                table: "store_credit_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_store_credit_transactions_order_id",
                table: "store_credit_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_sub_categories_category_id",
                table: "sub_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_customer_id",
                table: "user_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_wishlist_items_customer_id",
                table: "wishlist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_wishlist_items_product_id",
                table: "wishlist_items");

            migrationBuilder.DropIndex(
                name: "IX_wishlist_items_product_id",
                table: "wishlist_items");

            migrationBuilder.DropIndex(
                name: "IX_store_credit_transactions_order_id",
                table: "store_credit_transactions");

            migrationBuilder.DropIndex(
                name: "IX_stock_reservations_product_id",
                table: "stock_reservations");

            migrationBuilder.DropIndex(
                name: "IX_review_helpful_votes_customer_id",
                table: "review_helpful_votes");

            migrationBuilder.DropIndex(
                name: "IX_return_requests_product_id",
                table: "return_requests");

            migrationBuilder.DropIndex(
                name: "IX_recently_viewed_products_product_id",
                table: "recently_viewed_products");

            migrationBuilder.DropIndex(
                name: "IX_products_sub_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_product_questions_customer_id",
                table: "product_questions");

            migrationBuilder.DropIndex(
                name: "IX_orders_address_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_order_snapshots_customer_id",
                table: "order_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_order_snapshots_order_id",
                table: "order_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_order_snapshot_items_order_snapshot_id",
                table: "order_snapshot_items");

            migrationBuilder.DropIndex(
                name: "IX_order_snapshot_items_product_id",
                table: "order_snapshot_items");

            migrationBuilder.DropIndex(
                name: "IX_coupon_usages_customer_id",
                table: "coupon_usages");

            migrationBuilder.DropIndex(
                name: "IX_coupon_usages_order_id",
                table: "coupon_usages");

            migrationBuilder.DropIndex(
                name: "IX_collection_items_product_id",
                table: "collection_items");

            migrationBuilder.DropIndex(
                name: "IX_cart_items_product_id",
                table: "cart_items");

            migrationBuilder.AddForeignKey(
                name: "FK_addresses_customers_customer_id",
                table: "addresses",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_carts_cart_id",
                table: "cart_items",
                column: "cart_id",
                principalTable: "carts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_carts_customers_customer_id",
                table: "carts",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_order_id",
                table: "order_items",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_products_product_id",
                table: "order_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_customer_id",
                table: "orders",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_reviews_products_product_id",
                table: "product_reviews",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_wishlist_items_customers_customer_id",
                table: "wishlist_items",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
