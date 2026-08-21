using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Seed
{
    // E3 - LEGAL ICERIK TOHUMLAMA (idempotent).
    //
    // NEDEN VAR: storefront 10 sozlesme sayfasina link veriyor (#/sozlesme/<slug>) ama `contents`
    // tablosu BOSTU ve hicbir yerde tohumlama yoktu (olculdu: Seed/, migration HasData ve
    // database/mssql/*.sql'de contents kaydi YOK). Metinler index.html icindeki `LEGAL` i18n
    // nesnesinde GOMULUYDU. Gomuluyu kaldirip API'ye baglamak, tohumlama olmadan 10 BOS legal
    // sayfa demek olurdu - canli bir vitrinde bos KVKK/mesafeli satis sayfasi gomulu metinden
    // kotudur.
    //
    // METINLERIN KAYNAGI: index.html'deki `LEGAL` nesnesinden BIREBIR cikarildi (tarayicida
    // sayfanin KENDI esc() fonksiyonu kullanilarak, kacislama birebir ayni olsun diye). Metin
    // UYDURULMADI; yalniz bicim `<h3>baslik</h3>` + `<p>govde</p>` HTML'ine cevrildi - bu,
    // showLegal()'in bugun urettigi ciktinin ayni yapisi.
    //
    // IDEMPOTENTLIK SOZLESMESI (PINLI): slug ZATEN VARSA DOKUNULMAZ. Admin'in CMS'ten yaptigi
    // duzenleme sonraki uygulama acilisinda ASLA ezilmez. Yalniz EKSIK slug'lar eklenir.
    public class ContentSeeder
    {
        private readonly IContentDal _contentDal;
        private readonly ILogger<ContentSeeder> _logger;

        public ContentSeeder(IContentDal contentDal, ILogger<ContentSeeder> logger)
        {
            _contentDal = contentDal;
            _logger = logger;
        }

        // Tohum kaydi - salt okunur veri.
        public sealed record TohumIcerik(string Slug, string TitleTr, string TitleEn, string BodyTr, string BodyEn);

        // Test edilebilirlik icin PUBLIC: "tohum govdeleri Sanitize()'dan DEGISMEDEN gecer" pini
        // bu listeyi dogrudan okur (veritabani gerektirmeden).
        public static IReadOnlyList<TohumIcerik> Tohumlar => _tohumlar;

        public async Task SeedAsync()
        {
            var eklenen = 0;
            var atlanan = 0;

            foreach (var t in _tohumlar)
            {
                var mevcut = await _contentDal.GetBySlugAsync(t.Slug);
                if (mevcut != null)
                {
                    // ONEMLI: burada GUNCELLEME YAPILMAZ. Admin CMS'ten degistirdiyse o kazanir.
                    atlanan++;
                    continue;
                }

                await _contentDal.AddAsync(new Content
                {
                    slug = t.Slug,
                    title_tr = t.TitleTr,
                    title_en = t.TitleEn,
                    body_tr = t.BodyTr,
                    body_en = t.BodyEn,
                    is_active = true,
                    created_at = DateTime.Now
                });
                eklenen++;
            }

            if (eklenen > 0)
                _logger.LogInformation("Legal icerik tohumlama: {Eklenen} eklendi, {Atlanan} zaten vardi (dokunulmadi).", eklenen, atlanan);
        }

        // ── TOHUM VERISI (index.html LEGAL nesnesinden birebir) ────────────────────────────
        private static readonly TohumIcerik[] _tohumlar = new[]
        {
            new TohumIcerik("erisilebilirlik", "Erişilebilirlik Beyanı", "Accessibility Statement",
                "<h3>Taahhüdümüz</h3>\n<p>Divisima, tüm kullanıcılar için erişilebilir bir alışveriş deneyimi sunmayı taahhüt eder. Engelli kullanıcılar dâhil herkesin siteyi rahatça kullanabilmesi için sürekli iyileştirme yapıyoruz.</p>\n<h3>Uygunluk Durumu</h3>\n<p>Site, WCAG 2.1 AA (Web İçeriği Erişilebilirlik Kılavuzu) düzeyini hedefler. Renk kontrastı, klavye erişimi ve ekran okuyucu uyumu bu standarda göre tasarlanmıştır.</p>\n<h3>Aldığımız Önlemler</h3>\n<p>Tam klavye navigasyonu ve görünür odak halkaları; anlamlı ARIA etiketleri ile canlı bölge duyuruları; içeriğe atlama bağlantısı; açık ve koyu temada en az 4.5:1 renk kontrastı; modal pencerelerde odak yakalama; form alanlarında etiket ve hata durumu bildirimi. Bu önlemler siteyi klavye, ekran okuyucu ve büyüteç kullanıcıları için kullanılabilir kılar.</p>\n<h3>Bilinen Sınırlamalar</h3>\n<p>360° ürün görselleri ve bazı zengin medya öğeleri için erişilebilir alternatifler geliştirilme aşamasındadır. Ekran okuyucularla manuel testler (NVDA, VoiceOver) sürmektedir. Erişilebilirlikte bir engel yaşarsanız bize bildirin, en kısa sürede çözüme kavuşturalım.</p>\n<h3>Geri Bildirim</h3>\n<p>Erişilebilirlik konusundaki görüş ve önerileriniz için İletişim sayfamızdan bize ulaşabilirsiniz. Geri bildiriminiz, siteyi herkes için daha iyi hâle getirmemize yardımcı olur.</p>",
                "<h3>Our Commitment</h3>\n<p>Divisima is committed to providing an accessible shopping experience for all users. We continually work to ensure everyone, including people with disabilities, can use the site with ease.</p>\n<h3>Conformance Status</h3>\n<p>The site targets WCAG 2.1 AA (Web Content Accessibility Guidelines). Colour contrast, keyboard access and screen-reader compatibility are designed to this standard.</p>\n<h3>Measures We Take</h3>\n<p>Full keyboard navigation with visible focus rings; meaningful ARIA labels with live-region announcements; a skip-to-content link; a minimum 4.5:1 colour contrast in both light and dark themes; focus trapping in modals; labels and error announcements on form fields. These make the site usable for keyboard, screen-reader and magnifier users.</p>\n<h3>Known Limitations</h3>\n<p>Accessible alternatives for 360-degree product imagery and some rich-media elements are in development. Manual screen-reader testing (NVDA, VoiceOver) is ongoing. If you encounter an accessibility barrier, please let us know and we will address it promptly.</p>\n<h3>Feedback</h3>\n<p>For any accessibility feedback or suggestions, please reach us via our Contact page. Your feedback helps us make the site better for everyone.</p>"),

            new TohumIcerik("mesafeli-satis", "Mesafeli Satış Sözleşmesi", "Distance Sales Agreement",
                "<h3>Taraflar</h3>\n<p>İşbu sözleşme, Divisima (Satıcı) ile siparişi veren Alıcı arasında, 6502 sayılı Tüketicinin Korunması Hakkında Kanun ve Mesafeli Sözleşmeler Yönetmeliği uyarınca elektronik ortamda kurulur.</p>\n<h3>Sözleşme Konusu</h3>\n<p>Sözleşmenin konusu, Alıcı’nın internet sitesinden elektronik ortamda sipariş verdiği ürünün satışı ve teslimidir. Ürünün temel nitelikleri ve satış fiyatı sipariş sayfasında belirtilmiştir.</p>\n<h3>Teslimat</h3>\n<p>Ürün, siparişin onaylanmasını takiben en geç 30 gün içinde Alıcı’nın belirttiği adrese kargo ile teslim edilir. Kargo ücreti ve süresi sipariş özetinde gösterilir.</p>\n<h3>Cayma Hakkı</h3>\n<p>Alıcı, ürünü teslim aldığı tarihten itibaren 14 gün içinde herhangi bir gerekçe göstermeksizin ve cezai şart ödemeksizin sözleşmeden cayma hakkına sahiptir.</p>\n<h3>Uyuşmazlık</h3>\n<p>Uyuşmazlıklarda Alıcı’nın yerleşim yerindeki Tüketici Hakem Heyetleri ve Tüketici Mahkemeleri yetkilidir.</p>",
                "<h3>Parties</h3>\n<p>This agreement is concluded electronically between Divisima (Seller) and the ordering Buyer pursuant to Turkish Consumer Protection Law No. 6502 and the Distance Contracts Regulation.</p>\n<h3>Subject</h3>\n<p>The subject is the sale and delivery of the product ordered electronically from the website. The essential qualities and price are stated on the order page.</p>\n<h3>Delivery</h3>\n<p>The product is delivered by courier to the Buyer’s address within 30 days at the latest following order confirmation.</p>\n<h3>Right of Withdrawal</h3>\n<p>The Buyer may withdraw within 14 days from delivery without giving any reason or paying a penalty.</p>\n<h3>Disputes</h3>\n<p>For disputes, the Consumer Arbitration Committees and Consumer Courts at the Buyer’s residence have jurisdiction.</p>"),

            new TohumIcerik("iade", "İade & İptal Koşulları", "Returns & Cancellation",
                "<h3>14 Gün İade</h3>\n<p>Satın aldığın ürünü teslim tarihinden itibaren 14 gün içinde iade edebilirsin. Ürünün kullanılmamış, etiketli ve orijinal ambalajında olması gerekir.</p>\n<h3>İade Süreci</h3>\n<p>İade talebini hesabından veya iletişim kanallarından oluştur, ürünü anlaşmalı kargoyla ücretsiz gönder. Onay sonrası ödemen 10 iş günü içinde iade edilir.</p>\n<h3>İade Edilemeyen Ürünler</h3>\n<p>İç giyim, küpe gibi hijyen koşulu taşıyan ürünler ile kişiye özel üretilen ürünler iade kapsamı dışındadır.</p>\n<h3>Değişim</h3>\n<p>Beden veya renk değişimi için ürünü iade edip yeni siparişini oluşturman yeterlidir.</p>",
                "<h3>14-Day Returns</h3>\n<p>You may return your purchase within 14 days of delivery. The item must be unused, tagged and in its original packaging.</p>\n<h3>Return Process</h3>\n<p>Create a return request from your account or contact channels and ship the item free via our contracted courier. After approval, your payment is refunded within 10 business days.</p>\n<h3>Non-returnable</h3>\n<p>Underwear, earrings and hygiene-sensitive items, as well as custom-made products, are excluded.</p>\n<h3>Exchange</h3>\n<p>For size or colour exchange, simply return the item and place a new order.</p>"),

            new TohumIcerik("kvkk", "KVKK Aydınlatma Metni", "Personal Data Protection Notice",
                "<h3>Veri Sorumlusu</h3>\n<p>6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında veri sorumlusu Divisima’dır. Kişisel verilerin bu metinde açıklanan amaçlarla işlenir.</p>\n<h3>İşlenen Veriler</h3>\n<p>Ad-soyad, iletişim, teslimat adresi ve sipariş bilgilerin; üyelik, sipariş yönetimi ve yasal yükümlülüklerin yerine getirilmesi amacıyla işlenir.</p>\n<h3>Haklarınız</h3>\n<p>KVKK’nın 11. maddesi uyarınca verilerine erişme, düzeltme, silme ve işlemeye itiraz etme haklarına sahipsin.</p>\n<h3>Saklama Süresi</h3>\n<p>Verilerin, ilgili mevzuatta öngörülen süreler ve işleme amacının gerektirdiği süre boyunca saklanır.</p>",
                "<h3>Data Controller</h3>\n<p>Under Turkish Law No. 6698, the data controller is Divisima. Your personal data is processed for the purposes described here.</p>\n<h3>Processed Data</h3>\n<p>Your name, contact, delivery address and order details are processed for membership, order management and legal obligations.</p>\n<h3>Your Rights</h3>\n<p>Under Article 11, you have the right to access, correct, delete and object to the processing of your data.</p>\n<h3>Retention</h3>\n<p>Your data is retained for the periods required by law and by the processing purpose.</p>"),

            new TohumIcerik("gizlilik", "Gizlilik Politikası", "Privacy Policy",
                "<h3>Bilgi Toplama</h3>\n<p>Siteyi kullanırken paylaştığın bilgileri yalnızca siparişini işlemek, deneyimini iyileştirmek ve seni bilgilendirmek için kullanırız.</p>\n<h3>Bilgi Paylaşımı</h3>\n<p>Kişisel bilgilerin, yasal zorunluluklar ve hizmet sağlayıcılar (kargo, ödeme) dışında üçüncü kişilerle paylaşılmaz veya satılmaz.</p>\n<h3>Güvenlik</h3>\n<p>Ödeme işlemlerin 256-bit SSL ile şifrelenir; kart bilgilerin sitemizde saklanmaz.</p>",
                "<h3>Information Collection</h3>\n<p>We use the information you share only to process your order, improve your experience and keep you informed.</p>\n<h3>Sharing</h3>\n<p>Your personal information is never shared or sold except legal obligations and service providers (courier, payment).</p>\n<h3>Security</h3>\n<p>Your payments are encrypted with 256-bit SSL; your card details are not stored on our site.</p>"),

            new TohumIcerik("cerez", "Çerez Politikası", "Cookie Policy",
                "<h3>Çerez Nedir?</h3>\n<p>Çerezler, siteyi ziyaret ettiğinde cihazına kaydedilen küçük metin dosyalarıdır. Deneyimini kişiselleştirmek ve siteyi geliştirmek için kullanılır.</p>\n<h3>Kullandığımız Çerezler</h3>\n<p>Zorunlu çerezler sitenin çalışması için gereklidir. Performans ve pazarlama çerezleri isteğe bağlıdır ve onayınla etkinleşir.</p>\n<h3>Çerez Yönetimi</h3>\n<p>Tarayıcı ayarlarından çerezleri dilediğin zaman silebilir veya engelleyebilirsin.</p>",
                "<h3>What Are Cookies?</h3>\n<p>Cookies are small text files stored on your device when you visit the site, used to personalise your experience and improve the site.</p>\n<h3>Cookies We Use</h3>\n<p>Essential cookies are required for the site to function. Performance and marketing cookies are optional and activate with your consent.</p>\n<h3>Managing Cookies</h3>\n<p>You can delete or block cookies anytime from your browser settings.</p>"),

            new TohumIcerik("kargo", "Kargo & Teslimat", "Shipping & Delivery",
                "<h3>Teslimat Süresi</h3>\n<p>Siparişlerin, onaylandıktan sonra 1-3 iş günü içinde kargoya verilir ve genellikle 2-4 iş gününde teslim edilir.</p>\n<h3>Kargo Ücreti</h3>\n<p>2.500 ₺ ve üzeri siparişlerde kargo ücretsizdir. Altındaki siparişlerde sabit kargo ücreti sipariş özetinde gösterilir.</p>\n<h3>Takip</h3>\n<p>Kargon yola çıktığında takip numarası e-posta ve SMS ile paylaşılır.</p>",
                "<h3>Delivery Time</h3>\n<p>Orders ship within 1-3 business days after confirmation and typically arrive in 2-4 business days.</p>\n<h3>Shipping Fee</h3>\n<p>Shipping is free on orders of 2,500 TL and above. Below that, a flat fee is shown in the order summary.</p>\n<h3>Tracking</h3>\n<p>When your parcel ships, a tracking number is shared via email and SMS.</p>"),

            new TohumIcerik("sss", "Sıkça Sorulan Sorular", "FAQ",
                "<h3>Siparişimi nasıl takip ederim?</h3>\n<p>Kargon yola çıktığında ileteceğimiz takip numarasıyla kargo firmasının sitesinden takip edebilirsin. Ayrıca hesabındaki Siparişlerim bölümünden de durumu görebilirsin.</p>\n<h3>Kargo ne kadar sürede gelir?</h3>\n<p>Siparişlerin onaylandıktan sonra 1-3 iş günü içinde kargoya verilir ve genellikle 2-4 iş gününde adresine ulaşır.</p>\n<h3>Kargo ücreti ne kadar?</h3>\n<p>2.500 ₺ ve üzeri tüm siparişlerde kargo ücretsizdir. Bu tutarın altındaki siparişlerde sabit kargo ücreti sipariş özetinde gösterilir.</p>\n<h3>İade süreci nasıl işliyor?</h3>\n<p>Ürünü teslim tarihinden itibaren 14 gün içinde, kullanılmamış ve etiketli olarak anlaşmalı kargoyla ücretsiz iade edebilirsin. Onaydan sonra ödemen 10 iş günü içinde iade edilir.</p>\n<h3>Beden veya renk değişimi yapabilir miyim?</h3>\n<p>Elbette. Değişim için mevcut ürünü iade edip istediğin beden veya renkte yeni siparişini oluşturman yeterli.</p>\n<h3>Hangi ödeme yöntemleri var?</h3>\n<p>Kredi/banka kartı, kapıda ödeme ve havale/EFT ile ödeyebilirsin. Tüm kart ödemeleri 256-bit SSL ile güvence altındadır.</p>\n<h3>Taksit yapabilir miyim?</h3>\n<p>Kredi kartı ödemelerinde ürün ve banka koşullarına göre taksit imkânı sunulur; taksitli tutar ürün sayfasında gösterilir.</p>\n<h3>Doğru bedeni nasıl seçerim?</h3>\n<p>Her ürün sayfasındaki “Beden Tablosu” bağlantısından göğüs, bel ve kalça ölçülerini cm cinsinden inceleyebilir, model ölçülerini görebilirsin.</p>\n<h3>Üye olmadan sipariş verebilir miyim?</h3>\n<p>Evet. Ödeme sayfasında misafir olarak devam edebilirsin; sipariş bilgilerin belirttiğin e-posta adresine gönderilir.</p>\n<h3>Ürünlerin bakımı nasıl olmalı?</h3>\n<p>Her ürünün bakım talimatları ürün sayfasındaki “Bakım” bölümünde yer alır. İpek ve deri ürünler için kuru temizleme öneririz.</p>",
                "<h3>How do I track my order?</h3>\n<p>Once shipped, track it on the courier’s site using the number we send you. You can also check the status under My Orders in your account.</p>\n<h3>How long does shipping take?</h3>\n<p>Orders are shipped within 1-3 business days after confirmation and usually arrive in 2-4 business days.</p>\n<h3>How much is shipping?</h3>\n<p>Shipping is free on all orders of 2,500 TL and above. Below that, a flat shipping fee is shown in the order summary.</p>\n<h3>How do returns work?</h3>\n<p>Return unused, tagged items free within 14 days of delivery via our contracted courier. After approval, your refund is issued within 10 business days.</p>\n<h3>Can I exchange size or colour?</h3>\n<p>Of course. For an exchange, simply return the current item and place a new order in the size or colour you want.</p>\n<h3>What payment methods are available?</h3>\n<p>You can pay by credit/debit card, cash on delivery or bank transfer. All card payments are secured with 256-bit SSL.</p>\n<h3>Can I pay in instalments?</h3>\n<p>Instalments are available on credit card payments depending on the product and your bank; the instalment amount is shown on the product page.</p>\n<h3>How do I choose the right size?</h3>\n<p>Use the “Size Guide” link on each product page to see bust, waist and hip measurements in cm, along with the model’s measurements.</p>\n<h3>Can I order without an account?</h3>\n<p>Yes. You can continue as a guest at checkout; your order details are sent to the email address you provide.</p>\n<h3>How should I care for the products?</h3>\n<p>Care instructions for each item are in the “Care” section on the product page. We recommend dry cleaning for silk and leather items.</p>"),

            new TohumIcerik("iletisim", "İletişim", "Contact",
                "<h3>Müşteri Hizmetleri</h3>\n<p>Soruların için hafta içi 09:00-18:00 arası bize ulaşabilirsin. E-posta: destek@divisima.com</p>\n<h3>Adres</h3>\n<p>Divisima Tasarım, Nişantaşı, Şişli / İstanbul</p>\n<h3>Not</h3>\n<p>Bu bir tasarım simülasyonudur; iletişim bilgileri temsilidir.</p>",
                "<h3>Customer Service</h3>\n<p>Reach us on weekdays 09:00-18:00. Email: destek@divisima.com</p>\n<h3>Address</h3>\n<p>Divisima Design, Nişantaşı, Şişli / Istanbul</p>\n<h3>Note</h3>\n<p>This is a design simulation; contact details are illustrative.</p>"),

            new TohumIcerik("siparis-takibi", "Sipariş Takibi", "Order Tracking",
                "<h3>Siparişini Takip Et</h3>\n<p>Sipariş numaranı ve e-postanı girerek siparişinin durumunu görüntüleyebilirsin. (Simülasyon)</p>\n<h3>Yardım</h3>\n<p>Takip numaranı bulamıyorsan iletişim kanallarımızdan bize ulaşabilirsin.</p>",
                "<h3>Track Your Order</h3>\n<p>Enter your order number and email to view your order status. (Simulation)</p>\n<h3>Help</h3>\n<p>If you cannot find your tracking number, contact us via our channels.</p>"),
        };
    }
}
