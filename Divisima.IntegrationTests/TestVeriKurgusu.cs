using System;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.IntegrationTests
{
    // === D-SEMA-FIX: KURGULAR URETIMLE AYNI SOZLESMEYE UYAR ==============================
    //
    // Referans butunlugu DB duzeyine indikten sonra (53 FK) dogrudan `ctx.Add(...)` yapan
    // test kurgulari, uretimin ASLA uretmeyecegi satirlar yaziyordu:
    //     ReserveStock(..., orderId: 5001)      -> boyle bir siparis YOK
    //     new StockNotificationRequest { product_id = 1 }  -> boyle bir urun YOK
    // Uretimde ikisi de imkansizdir: rezervasyonu OrderManager az once yazdigi siparisin
    // id'siyle acar, aboneligi de katalogdan gelen gercek bir urune baglar.
    //
    // KARAR (Sprint 8 madde 10 kalibi): kolonu/kisiti GEVSETMEK yerine KURGU duzeltilir.
    // Oradaki gerekce birebir gecerli: "tokensiz bir satir hicbir zaman abonelikten
    // cikarilamaz - o yuzden kolon opsiyonel BIRAKILMADI ve dogrudan insert yapan test
    // kurgulari da uretimle ayni sozlesmeye uyuyor."
    //
    // Bu sinif o kurgulari TEK YERDE toplar; uc ayri test dosyasi ayni yardimciyi kullanir.
    internal static class TestVeriKurgusu
    {
        private static string Damga() => Guid.NewGuid().ToString("N").Substring(0, 8);

        // GERCEK musteri + GERCEK siparis. Donen id, uretimdeki gibi var olan bir satirdir.
        public static async Task<int> GercekSiparisAsync(DivisimaDbContext ctx, byte durum = 0)
        {
            var damga = Damga();
            var musteri = new Customer
            {
                name = "Kurgu Musteri " + damga,
                email = $"kurgu.{damga}@example.com",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(musteri);
            await ctx.SaveChangesAsync();

            var siparis = new Order
            {
                customer_id = musteri.id,
                order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + damga,
                status = durum,
                subtotal = 100m,
                discount_amount = 0m,
                shipping_cost = 0m,
                total_price = 100m,
                currency = "TRY",
                payment_type = 1,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(siparis);
            await ctx.SaveChangesAsync();
            return siparis.id;
        }

        // GERCEK kategori + GERCEK urun. description ve color_hex ZORUNLUDUR
        // (CLAUDE.md bolum 5 - "bilinen tuzaklar").
        public static async Task<int> GercekUrunAsync(DivisimaDbContext ctx)
        {
            var damga = Damga();
            var kategori = new Category
            {
                name = "Kurgu Kategori " + damga,
                slug = "kurgu-" + damga,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Kurgu Urun " + damga,
                description = "Test kurgusu icin urun.",
                color_hex = "#334455",
                brand = "Divisima",
                price = 499.90m,
                category_id = kategori.id,
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(urun);
            await ctx.SaveChangesAsync();
            return urun.id;
        }
    }
}
