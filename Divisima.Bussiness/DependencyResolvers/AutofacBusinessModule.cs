using Autofac;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.DataAccess;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete;
using Divisima.DataAccess.Concrete.EntityFramework;

namespace Divisima.Bussiness.DependencyResolvers.Autofac
{
    // Açıklayıcı yorum: Autofac DI modülü. Servis ve DAL kayıtları. Cafixo AutofacBusinessModule kalıbı.
    // InstancePerLifetimeScope: her HTTP isteği için tek örnek. Modüller eklendikçe kayıtlar genişleyecek.
    public class AutofacBusinessModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Açıklayıcı yorum: Ürün modülü kayıtları
            builder.RegisterType<ProductManager>().As<IProductService>().InstancePerLifetimeScope();
            builder.RegisterType<EfProductDal>().As<IProductDal>().InstancePerLifetimeScope();
            // ── Satıcı (marketplace) modülü ──
            builder.RegisterType<SellerAuthManager>().As<ISellerAuthService>().InstancePerLifetimeScope();
            builder.RegisterType<SellerManager>().As<ISellerService>().InstancePerLifetimeScope();
            builder.RegisterType<EfSellerDal>().As<ISellerDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfConsentRecordDal>().As<IConsentRecordDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfProductStockDal>().As<IProductStockDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Kategori modülü kayıtları
            builder.RegisterType<CategoryManager>().As<ICategoryService>().InstancePerLifetimeScope();
            builder.RegisterType<EfCategoryDal>().As<ICategoryDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfSubCategoryDal>().As<ISubCategoryDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Kupon modülü kayıtları
            builder.RegisterType<CouponManager>().As<ICouponService>().InstancePerLifetimeScope();
            builder.RegisterType<EfCouponDal>().As<ICouponDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfCouponUsageDal>().As<ICouponUsageDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Koleksiyon modülü kayıtları
            builder.RegisterType<CollectionManager>().As<ICollectionService>().InstancePerLifetimeScope();
            builder.RegisterType<EfCollectionDal>().As<ICollectionDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfCollectionItemDal>().As<ICollectionItemDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Unit of Work (transaction) kaydı
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Müşteri DAL kaydı
            builder.RegisterType<EfCustomerDal>().As<ICustomerDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Stok modülü kayıtları
            builder.RegisterType<StockManager>().As<IStockService>().InstancePerLifetimeScope();
            // Açıklayıcı yorum: Öneri motoru (yeni DAL yok - mevcut OrderItem/Product/Category DAL kullanır)
            builder.RegisterType<RecommendationManager>().As<IRecommendationService>().InstancePerLifetimeScope();
            // Açıklayıcı yorum: Stok bildirim ("gelince haber ver") - manager + yeni DAL
            builder.RegisterType<StockNotificationManager>().As<IStockNotificationService>().InstancePerLifetimeScope();
            builder.RegisterType<EfStockNotificationRequestDal>().As<IStockNotificationRequestDal>().InstancePerLifetimeScope();
            // Açıklayıcı yorum: Sipariş durum geçmişi (zaman çizelgesi)
            builder.RegisterType<OrderStatusHistoryManager>().As<IOrderStatusHistoryService>().InstancePerLifetimeScope();
            builder.RegisterType<EfOrderStatusHistoryDal>().As<IOrderStatusHistoryDal>().InstancePerLifetimeScope();
            // Açıklayıcı yorum: Terk edilmiş sepet hatırlatması (yeni DAL yok - mevcut Cart/CartItem/Customer DAL)
            // İYS/ETK kapısı - pazarlama iletilerinin TEK karar noktası (terk-sepet, doğum günü,
            // win-back, yorum daveti, fiyat düşüşü). İşlemsel mailler bu kapıdan geçmez.
            builder.RegisterType<MarketingGate>().As<IMarketingGate>().InstancePerLifetimeScope();

            builder.RegisterType<AbandonedCartManager>().As<IAbandonedCartService>().InstancePerLifetimeScope();
            // Açıklayıcı yorum: Son görüntülenen ürünler
            builder.RegisterType<RecentlyViewedManager>().As<IRecentlyViewedService>().InstancePerLifetimeScope();
            // === Dalga 1: Hesap yönetimi ===
            builder.RegisterType<AccountManager>().As<IAccountService>().InstancePerLifetimeScope();
            // === Dalga 2-3: Sadakat + Kredi + Hediye kartı ===
            builder.RegisterType<LoyaltyManager>().As<ILoyaltyService>().InstancePerLifetimeScope();
            builder.RegisterType<EfLoyaltyTransactionDal>().As<ILoyaltyTransactionDal>().InstancePerLifetimeScope();
            builder.RegisterType<StoreCreditManager>().As<IStoreCreditService>().InstancePerLifetimeScope();
            builder.RegisterType<EfStoreCreditTransactionDal>().As<IStoreCreditTransactionDal>().InstancePerLifetimeScope();
            builder.RegisterType<GiftCardManager>().As<IGiftCardService>().InstancePerLifetimeScope();
            builder.RegisterType<EfGiftCardDal>().As<IGiftCardDal>().InstancePerLifetimeScope();
            // === Dalga 4: Fiyat düşüş bildirimi ===
            builder.RegisterType<PriceDropManager>().As<IPriceDropService>().InstancePerLifetimeScope();
            builder.RegisterType<EfPriceDropSubscriptionDal>().As<IPriceDropSubscriptionDal>().InstancePerLifetimeScope();
            // === Dalga 5: Yorum güçlendirme ===
            builder.RegisterType<EfReviewHelpfulVoteDal>().As<IReviewHelpfulVoteDal>().InstancePerLifetimeScope();
            builder.RegisterType<ProductQuestionManager>().As<IProductQuestionService>().InstancePerLifetimeScope();
            builder.RegisterType<EfProductQuestionDal>().As<IProductQuestionDal>().InstancePerLifetimeScope();
            // === Dalga 6: Etkileşim kampanyaları ===
            builder.RegisterType<EngagementManager>().As<IEngagementService>().InstancePerLifetimeScope();
            builder.RegisterType<ReferralManager>().As<IReferralService>().InstancePerLifetimeScope();
            // === Dalga 8: Vitrin listeleri ===
            builder.RegisterType<MerchandisingManager>().As<IMerchandisingService>().InstancePerLifetimeScope();
            // === Dalga 11: Ürün özellikleri (faceted search) ===
            builder.RegisterType<ProductAttributeManager>().As<IProductAttributeService>().InstancePerLifetimeScope();
            builder.RegisterType<EfProductAttributeDal>().As<IProductAttributeDal>().InstancePerLifetimeScope();
            // === Dalga 12: Beden rehberi + karşılaştırma ===
            builder.RegisterType<SizeGuideManager>().As<ISizeGuideService>().InstancePerLifetimeScope();
            builder.RegisterType<EfSizeGuideEntryDal>().As<ISizeGuideEntryDal>().InstancePerLifetimeScope();
            builder.RegisterType<ProductComparisonManager>().As<IProductComparisonService>().InstancePerLifetimeScope();
            // === Dalga 12: Misafir checkout ===
            builder.RegisterType<GuestCheckoutManager>().As<IGuestCheckoutService>().InstancePerLifetimeScope();
            builder.RegisterType<EfRecentlyViewedProductDal>().As<IRecentlyViewedProductDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfStockMovementDal>().As<IStockMovementDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Sipariş modülü kayıtları (Order zinciri)
            builder.RegisterType<OrderManager>().As<IOrderService>().InstancePerLifetimeScope();
            builder.RegisterType<EfOrderDal>().As<IOrderDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfOrderItemDal>().As<IOrderItemDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfOrderSnapshotDal>().As<IOrderSnapshotDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfOrderSnapshotItemDal>().As<IOrderSnapshotItemDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Sipariş event pipeline (publisher + handler'lar)
            builder.RegisterType<OrderPlacedEventPublisher>().As<IOrderPlacedEventPublisher>().InstancePerLifetimeScope();
            builder.RegisterType<OrderPlacedLogHandler>().As<IOrderPlacedEventHandler>().InstancePerLifetimeScope();
            // Açıklayıcı yorum: Faz 2 - OrderPlacedEmailHandler, OrderPlacedNotificationHandler buraya eklenecek

            // Açıklayıcı yorum: Ürün yorumu modülü kayıtları
            builder.RegisterType<ProductReviewManager>().As<IProductReviewService>().InstancePerLifetimeScope();
            builder.RegisterType<EfProductReviewDal>().As<IProductReviewDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Auth modülü kayıtları (JWT + session)
            builder.RegisterType<AuthManager>().As<IAuthService>().InstancePerLifetimeScope();
            builder.RegisterType<EfUserSessionDal>().As<IUserSessionDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: İçerik modülü kayıtları
            builder.RegisterType<ContentManager>().As<IContentService>().InstancePerLifetimeScope();
            builder.RegisterType<EfContentDal>().As<IContentDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: JwtHelper token helper olarak (Cafixo JwtHelper : ITokenHelper)
            // builder.RegisterType<JwtHelper>().As<ITokenHelper>().InstancePerLifetimeScope();  // Core katmanında register edilir

            // Açıklayıcı yorum: Sepet modülü kayıtları
            builder.RegisterType<CartManager>().As<ICartService>().InstancePerLifetimeScope();
            builder.RegisterType<EfCartDal>().As<ICartDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfCartItemDal>().As<ICartItemDal>().InstancePerLifetimeScope();

            // Açıklayıcı yorum: Kalan (opsiyonel): Address, Customer profil servisi
            // ── Outbox (garantili event) ──
            builder.RegisterType<OutboxService>().As<IOutboxService>().InstancePerLifetimeScope();
            // SPRINT 8 MADDE 3: odeme onayi yan etkileri (fatura + sadakat + referans + kupon sayaci).
            builder.RegisterType<Divisima.Bussiness.Events.PaymentConfirmedSideEffects>()
                .As<Divisima.Bussiness.Events.IPaymentConfirmedSideEffects>().InstancePerLifetimeScope();
            builder.RegisterType<OutboxProcessor>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<Divisima.Bussiness.Seed.AdminSeeder>().AsSelf().InstancePerLifetimeScope();
            // E3: legal icerik tohumlayici (idempotent - mevcut slug'a DOKUNMAZ).
            builder.RegisterType<Divisima.Bussiness.Seed.ContentSeeder>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<EfOutboxMessageDal>().As<IOutboxMessageDal>().InstancePerLifetimeScope();

            // ── Fraud/hız kontrolü ──
            builder.RegisterType<FraudCheckManager>().As<IFraudCheckService>().SingleInstance();

            // ── Ödeme (Iyzico) ──
            builder.RegisterType<IyzicoPaymentManager>().As<IPaymentService>().InstancePerLifetimeScope();
            builder.RegisterType<EfPaymentDal>().As<IPaymentDal>().InstancePerLifetimeScope();

            // ── Adres defteri ──
            builder.RegisterType<AddressManager>().As<IAddressService>().InstancePerLifetimeScope();
            builder.RegisterType<EfAddressDal>().As<IAddressDal>().InstancePerLifetimeScope();

            // ── Kalıcı sepet ──

            // ── Arama ──
            builder.RegisterType<SearchManager>().As<ISearchService>().InstancePerLifetimeScope();

            // ── Veri saklama/temizlik ──
            builder.RegisterType<DataRetentionJob>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<Divisima.Bussiness.Jobs.ReservationCleanupJob>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<EfStockReservationDal>().As<IStockReservationDal>().InstancePerLifetimeScope();

            // ── Kargo ──
            builder.RegisterType<ShipmentManager>().As<IShipmentService>().InstancePerLifetimeScope();
            builder.RegisterType<EfShipmentDal>().As<IShipmentDal>().InstancePerLifetimeScope();

            // ── Cihaz/push ──
            builder.RegisterType<CustomerDeviceManager>().As<ICustomerDeviceService>().InstancePerLifetimeScope();
            builder.RegisterType<EfCustomerDeviceDal>().As<ICustomerDeviceDal>().InstancePerLifetimeScope();

            // ── Fatura ──
            builder.RegisterType<InvoiceManager>().As<IInvoiceService>().InstancePerLifetimeScope();
            builder.RegisterType<EfInvoiceDal>().As<IInvoiceDal>().InstancePerLifetimeScope();
            builder.RegisterType<EfInvoiceItemDal>().As<IInvoiceItemDal>().InstancePerLifetimeScope();

            // ── İade/değişim ──
            builder.RegisterType<ReturnManager>().As<IReturnService>().InstancePerLifetimeScope();
            builder.RegisterType<RefundManager>().As<IRefundService>().InstancePerLifetimeScope();
            builder.RegisterType<OrderNotificationManager>().As<IOrderNotificationService>().InstancePerLifetimeScope();
            builder.RegisterType<EfReturnRequestDal>().As<IReturnRequestDal>().InstancePerLifetimeScope();

            // ── Ürün görsel ──
            builder.RegisterType<ProductImageManager>().As<IProductImageService>().InstancePerLifetimeScope();
            builder.RegisterType<EfProductImageDal>().As<IProductImageDal>().InstancePerLifetimeScope();

            // ── Admin müşteri yönetimi ──
            builder.RegisterType<AdminCustomerManager>().As<IAdminCustomerService>().InstancePerLifetimeScope();

            // ── Dashboard/rapor (admin) ──
            builder.RegisterType<DashboardManager>().As<IDashboardService>().InstancePerLifetimeScope();

            // ── Güvenlik olayları ──
            builder.RegisterType<SecurityEventManager>().As<ISecurityEventService>().InstancePerLifetimeScope();
            builder.RegisterType<EfSecurityEventDal>().As<ISecurityEventDal>().InstancePerLifetimeScope();

            // ── Denetim kaydı ──
            // FAZ 0 / K6: kayit KONVANSIYONEL DEGIL, ACIK (bu modulde her servis tek tek
            // RegisterType<X>().As<IY>() ile kayitli - RegisterAssemblyTypes yok). Bu yuzden
            // AuditLogManager icin ACIK satir zorunlu; olmazsa controller'in kurucusu
            // cozulemez ve uc calisma aninda patlar.
            builder.RegisterType<AuditLogManager>().As<IAuditLogService>().InstancePerLifetimeScope();
            builder.RegisterType<EfAuditLogDal>().As<IAuditLogDal>().InstancePerLifetimeScope();

            // ── Favoriler ──
            builder.RegisterType<WishlistManager>().As<IWishlistService>().InstancePerLifetimeScope();
            builder.RegisterType<EfWishlistItemDal>().As<IWishlistItemDal>().InstancePerLifetimeScope();

            // ── Event handler'ları (OrderPlaced pipeline) ──
            builder.RegisterType<OrderPlacedEmailHandler>().As<IOrderPlacedEventHandler>().InstancePerLifetimeScope();
            builder.RegisterType<OrderPlacedNotificationHandler>().As<IOrderPlacedEventHandler>().InstancePerLifetimeScope();

            builder.RegisterType<OrderConfirmationManager>().As<IOrderConfirmationService>().InstancePerLifetimeScope();

        }
    }
}
