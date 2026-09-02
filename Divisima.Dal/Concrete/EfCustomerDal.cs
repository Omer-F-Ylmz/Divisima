using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Müşteri DAL implementasyonu.
    public class EfCustomerDal : EfEntityRepositoryBase<Customer, DivisimaDbContext>, ICustomerDal
    {
        public EfCustomerDal(DivisimaDbContext context) : base(context)
        {
        }

        // Açıklayıcı yorum: E-posta ile müşteri (login + kayıt duplikat kontrolü)
        //
        // ══ KALITE SUPURMESI B1 - KIMLIK DIZGESI, KULTURSUZ KARSILASTIRILIR ═════════════════
        // ONCEKI HALI: `.ToLower()` (kultur duyarli) + `c.email.ToLower()` (SQL LOWER).
        // Uygulama tr-TR'ye PINLENDIGI icin (Sprint 8 madde 13) 'I' -> 'ı' (U+0131) oluyordu.
        // Veritabani collation'i Turkish_CI_AS ve OLCULDU: 'irem' = 'IREM' -> FARKLI
        // (Turkcede cift'ler I<->ı ve İ<->i; i ile I ayni harf DEGIL).
        // CANLI ZARAR: ayni adresin iki farkli yazimi IKI AYRI HESAP acti
        // (customers id 14 'ırıs.kalite@...' ve id 15 'iris.kalite@...'), ve kullanici ancak
        // KAYITTA yazdigi harf duzeniyle giris yapabiliyordu.
        //
        // E-posta bir KIMLIK dizgesidir, insan-gorunur bir metin degil: karsilastirmasi
        // KULTURDEN BAGIMSIZ olmali. Iki yari birden degisti:
        //  1) C# tarafi ToLowerInvariant,
        //  2) SQL tarafindaki LOWER() KALDIRILDI. Gerekce: SQL LOWER veritabani
        //     collation'ini (Turkish) kullanir; invariant normalize edilmis bir degerle
        //     karsilastirilinca yine ayrisir. Saklanan degerler artik HER ZAMAN invariant
        //     kucuk harf oldugu icin (bkz. AuthManager + normalize migration'i) dogrudan
        //     esitlik DOGRU ve ustelik INDEKS KULLANABILIR - LOWER(email) sarmalayicisi
        //     IX_customers_email indeksini kullanilamaz hale getiriyordu (yan kazanc).
        public async Task<Customer> GetByEmailAsync(string email)
        {
            var normalized = (email ?? "").Trim().ToLowerInvariant();
            return await Context.Set<Customer>()
                .FirstOrDefaultAsync(c => c.email == normalized);
        }

        // ══ GUVENLIK-FIX (G2) - GLOBAL FILTREYI ATLAYAN E-POSTA ARAMASI ══════════════════
        // GetByEmailAsync (yukarida) Customer'in GLOBAL `is_active` query filter'ina tabidir:
        // ASKIYA ALINMIS bir hesap ona NULL gorunur. Kayit yolu bunu "adres bos" diye okuyup
        // INSERT deniyordu ve IX_customers_email UNIQUE indeksine takilip HTTP 500 donuyordu.
        // OLCULDU: A (id=22) `is_active=0` yapildi -> ayni adresle kayit -> 500, tabloda satir
        // sayisi 1'de kaldi. Ustelik bu 500, G2 enumeration duzeltmesinden SONRA da ayirt
        // edilebilir bir yanit olurdu (201 vs 500) - yani sizintiyi acik birakirdi.
        // Normalizasyon BILEREK burada tekrarlaniyor: B1 kurali "kimlik dizgesi kultursuz
        // kucultulur" ve o kural DAL'in sorumlulugunda (cagri yerine dagitilmaz).
        public async Task<Customer> GetByEmailIgnoringFiltersAsync(string email)
        {
            var normalized = (email ?? "").Trim().ToLowerInvariant();
            return await Context.Set<Customer>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.email == normalized);
        }

        // Açıklayıcı yorum: ATOMİK düşüm - WHERE guard'ı DB'de değerlendirilir, iki eşzamanlı istek asla aynı bakiyeyi
        // iki kez harcayamaz (biri koşulu geçemez, 0 döner). row_version gerektirmez, diğer Customer yazma yollarını etkilemez.
        public async Task<int> TryDecrementStoreCreditAsync(int customerId, decimal amount)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId && c.store_credit >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.store_credit, c => c.store_credit - amount));
        }

        public async Task<int> TryDecrementLoyaltyPointsAsync(int customerId, int points)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId && c.loyalty_points >= points)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.loyalty_points, c => c.loyalty_points - points));
        }

        public async Task<int> IncrementStoreCreditAsync(int customerId, decimal amount)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.store_credit, c => c.store_credit + amount));
        }

        // Aciklayici yorum: ATOMIK puan ekleme - tek UPDATE (read-modify-write lost update'ini onler)
        public async Task<int> IncrementLoyaltyPointsAsync(int customerId, int points)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.loyalty_points, c => c.loyalty_points + points));
        }

        // Aciklayici yorum: ATOMIK basarisiz-login artisi + YENI degeri don (eszamanli denemeler artisi kaybetmez).
        public async Task<int> IncrementFailedLoginAsync(int customerId)
        {
            await Context.Set<Customer>().Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.failed_login_attempts, c => c.failed_login_attempts + 1));
            return await Context.Set<Customer>().Where(c => c.id == customerId)
                .Select(c => c.failed_login_attempts).FirstAsync();
        }

        public async Task LockAccountAsync(int customerId, DateTime until)
        {
            await Context.Set<Customer>().Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.lockout_end, until)
                    .SetProperty(c => c.failed_login_attempts, 0));
        }

        public async Task ResetLoginStateAsync(int customerId, DateTime lastLogin)
        {
            await Context.Set<Customer>().Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.failed_login_attempts, 0)
                    .SetProperty(c => c.lockout_end, (DateTime?)null)
                    .SetProperty(c => c.last_login_at, (DateTime?)lastLogin));
        }

        // ══ GF-1b / K10 (GF1-B10) - SIFIRLAMA JETONUNU ATOMIK TUKET ══════════════════════
        //
        // Jetonun GECERLILIGI ve TUKETILMESI TEK ifadede olur: `WHERE ozet = @ozet AND
        // expiry >= @simdi`. Boylece "oku - kontrol et - yaz" arasindaki pencere KAPANIR.
        //
        // SIFRE de AYNI ifadede yazilir. Ayri bir UPDATE'e birakilsaydi kaybeden istek
        // jetonu tuketemez ama sifreyi YAZABILIRDI - yani yaris yine kaybedilirdi.
        //
        // GLOBAL FILTRE BILINCLI OLARAK ACIK BIRAKILDI (IgnoreQueryFilters YOK): cagri
        // yerindeki okuma da ayni filtreye tabi; askiya alinmis bir hesap ORADA da
        // bulunamaz. Iki tarafin AYNI kumeye bakmasi sarttir.
        public async Task<int> TryConsumeResetTokenAsync(string tokenOzeti, DateTime simdi,
            byte[] hash, byte[] salt)
        {
            return await Context.Set<Customer>()
                .Where(c => c.password_reset_token == tokenOzeti
                            && c.password_reset_expiry != null
                            && c.password_reset_expiry >= simdi)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.password_hash, hash)
                    .SetProperty(c => c.password_salt, salt)
                    .SetProperty(c => c.password_reset_token, (string?)null)
                    .SetProperty(c => c.password_reset_expiry, (DateTime?)null)
                    .SetProperty(c => c.failed_login_attempts, 0)
                    .SetProperty(c => c.lockout_end, (DateTime?)null));
        }
    }
}
