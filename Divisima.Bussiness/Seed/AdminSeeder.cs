using Divisima.Core.Security.Hashing;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Seed
{
    // Açıklayıcı yorum: İlk admin tohumlama. Uygulama başlangıcında (Program.cs) bir kez çağrılır, IDEMPOTENT.
    // Yapılandırmadan (AdminSeed bölümü) okur; hiç admin yoksa oluşturur veya mevcut e-postayı admin'e yükseltir.
    // user_type alanı eklendiğinden beri admin oluşturmanın TEK güvenli yolu budur (elle SQL yerine).
    public class AdminSeeder
    {
        private readonly ICustomerDal _customerDal;
        private readonly IConfiguration _config;
        private readonly ILogger<AdminSeeder> _logger;

        public AdminSeeder(ICustomerDal customerDal, IConfiguration config, ILogger<AdminSeeder> logger)
        {
            _customerDal = customerDal;
            _config = config;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            // Açıklayıcı yorum: Güvenli varsayılan - yalnızca açıkça etkinleştirilirse çalışır (yanlışlıkla admin oluşturmayı önler)
            var enabled = bool.TryParse(_config["AdminSeed:Enabled"], out var e) && e;
            if (!enabled) return;

            var email = (_config["AdminSeed:Email"] ?? "").Trim().ToLowerInvariant();   // B1: KIMLIK dizgesi
            var password = _config["AdminSeed:Password"] ?? "";
            var name = string.IsNullOrWhiteSpace(_config["AdminSeed:Name"]) ? "Yönetici" : _config["AdminSeed:Name"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("AdminSeed etkin ama Email/Password boş - admin oluşturulmadı.");
                return;
            }

            // ══ DALGA C / C3 - SIFRE POLITIKASI BURADA DA UYGULANIR (BESINCI GIRIS NOKTASI) ══
            // A2-FIX (SUPHELI #21) sifre kuralini TEK MERKEZE tasidi ve DORT girise bagladi:
            // kayit, satici kaydi, sifre degistirme, sifre sifirlama. BU BESINCISIYDI ve
            // GOZDEN KACMISTI - yani sistemin EN YETKILI hesabi, kayit ucunun reddedecegi bir
            // sifreyle acilabiliyordu ("abc" gecerdi).
            //
            // FAIL-FAST SECILMEDI - GEREKCE: AdminSeed tek seferlik bir ONYUKLEME bayragidir.
            // Yanlis yazilmis bir sifre yuzunden uygulamanin ACILMAMASI, siteyi tumden indirir;
            // burada dogru davranis "admini OLUSTURMA ve GURULTULU soyle"dir. Bu, Program.cs'in
            // tohumlama hatasini yutup logladigi mevcut tasarimla da tutarli.
            //
            // Mesaj IHLAL EDILEN KURALI soyler (SifrePolitikasi zaten onu doner) - operator
            // neyi duzeltecegini bilmeden deneme yanilmaya dusmesin.
            var sifreIhlali = Divisima.Core.Security.SifrePolitikasi.Dogrula(password);
            if (sifreIhlali != null)
            {
                _logger.LogError(
                    "AdminSeed sifresi POLITIKAYA UYMUYOR, ilk admin OLUSTURULMADI: {Sebep} " +
                    "(AdminSeed:Password duzeltilip uygulama yeniden baslatilmali).", sifreIhlali);
                return;
            }

            // Açıklayıcı yorum: Zaten bir admin varsa hiçbir şey yapma (idempotent + istenmeyen ikinci admin'i önler)
            var existingAdmin = await _customerDal.GetAsync(c => c.user_type == (byte)UserTypeEnum.Admin);
            if (existingAdmin != null)
            {
                _logger.LogInformation("Admin zaten mevcut - tohumlama atlandı.");
                return;
            }

            // Açıklayıcı yorum: Bu e-posta zaten müşteri olarak kayıtlıysa admin'e YÜKSELT, değilse yeni admin OLUŞTUR
            var existing = await _customerDal.GetByEmailAsync(email);
            if (existing != null)
            {
                existing.user_type = (byte)UserTypeEnum.Admin;
                await _customerDal.UpdateAsync(existing);
                // GF-3/K2: admin e-postasi da PII'dir ve log'a duz gidiyordu (KVKK).
                _logger.LogInformation("Mevcut kullanıcı ({Email}) admin'e yükseltildi.",
                    Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(email));
                return;
            }

            HashingHelper.CreatePasswordHash(password, out var hash, out var salt);
            await _customerDal.AddAsync(new Customer
            {
                name = name,
                email = email,
                password_hash = hash,
                password_salt = salt,
                user_type = (byte)UserTypeEnum.Admin,
                is_active = true,
                email_verified = true,   // seed admin doğrulanmış kabul edilir
                phone = "05000000000",   // customers.phone NOT NULL - seed bunu doldurmuyordu
                created_at = DateTime.Now
            });
            // GF-3/K2: yukaridakiyle AYNI kusur, ayni dosyada ikinci nokta.
            _logger.LogInformation("İlk admin oluşturuldu ({Email}).",
                Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(email));
        }
    }
}
