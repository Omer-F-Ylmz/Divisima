using System.Net;
using System.Net.Http.Json;
using Divisima.Core.Security.Tokens;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ LF-5 - E-POSTA DOGRULAMA = 6 HANELI KOD ════════════════════════════════════════════
    //
    // BUNLAR DAVRANIS PINLERIDIR - kaynak metni degil, GERCEK HTTP YIGINI olculur: uc, model
    // baglama, DI, dagitik sayac ve veritabani birlikte kosar. Sahte DAL'la yazilsalardi
    // "kod dogru ama DI kaydi eksik" gibi bir kusuru YAKALAYAMAZLARDI.
    //
    // KOD NASIL BILINIYOR: uretilen kod YALNIZ e-postaya gider (bu dalganin butun amaci bu).
    // Test, kodun DUZ HALINI ogrenmeye CALISMAZ - bunun yerine BILINEN bir kodun ozetini
    // UYGULAMANIN KENDI servisiyle satira yazar. Ozetleme kuralinin ikinci kopyasi ACILMAZ.
    // HOST: `CustomWebApplicationFactory` KULLANILMAZ - o Testcontainers uzerinden DOCKER
    // ister ve bu makinede Docker YOK (bilinen uc kirmizinin sebebi). Bunun yerine
    // `AuthRateLimitPinTests`in Docker'siz kalibi izlenir: gercek `Program` host'u + SQL.
    [Trait("Category", "Sql")]
    public class LaunchFix5DogrulamaKoduTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaLf5DogrulamaTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        // ══ GF-3/F2 KAPISI - AD KOSUCU AD ALANINDAN GECER ══════════════════════════════════
        // ILK YAZIM `InitialCatalog`i SABIT `DbName` ile eziyordu; GF-3/F2 pini bunu ANINDA
        // yakaladi (isimli kirmizi, dosya adiyla birlikte). Tam korudugu kusur bu dalgada
        // ZATEN yasandi: es zamanli iki kosum ayni test veritabanina girdi ve
        // `There is already an object named 'audit_logs'` ile SAHTE kirmizi uretti.
        // Ad artik TEK URETIM NOKTASINDAN (`TestDbAdi.Cozumle`) gecer.
        private static string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(baseConn)
                { InitialCatalog = TestDbAdi.Cozumle(DbName) }.ConnectionString;
            }
        }

        private sealed class Lf5Factory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private Lf5Factory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new Lf5Factory();
                _ = _factory!.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak LF-5 test ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static string YeniEposta() => $"lf5-{Guid.NewGuid():N}@example.invalid";

        // Kayit acar ve BILINEN kodu satira yazar; duz kodu dondurur.
        private async Task<(string eposta, string kod)> KayitAcAsync(string? kodOverride = null,
                                                                     DateTime? gonderimZamani = null)
        {
            var eposta = YeniEposta();
            var kod = kodOverride ?? "314159";
            var anon = _factory!.CreateClient();

            var kayit = await anon.PostAsJsonAsync("/api/auth/register", new
            {
                name = "LF5 Test",
                email = eposta,
                phone = "5550000000",
                password = "GecerliParola1",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });
            kayit.StatusCode.Should().Be(HttpStatusCode.Created, "kayit ucu calismali");

            using var scope = _factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var kodServisi = scope.ServiceProvider.GetRequiredService<IDogrulamaKoduServisi>();
            var kucuk = eposta.ToLowerInvariant();
            var musteri = await db.Set<Customer>().FirstAsync(c => c.email == kucuk);
            musteri.email_verification_token = kodServisi.Ozetle(kod);
            musteri.email_verification_sent_at = gonderimZamani ?? DateTime.Now;
            await db.SaveChangesAsync();

            return (kucuk, kod);
        }

        private async Task<HttpResponseMessage> DogrulaAsync(string eposta, string kod) =>
            await _factory!.CreateClient().GetAsync(
                $"/api/auth/verify-email?email={Uri.EscapeDataString(eposta)}&code={Uri.EscapeDataString(kod)}");

        // ── (1) DOGRU KOD -> 200 ve hesap GERCEKTEN dogrulanir ────────────────────────────
        [Fact]
        public async Task DOGRU_KOD_200_DONER_ve_HESAP_DOGRULANIR()
        {
            if (Skipped()) return;
            var (eposta, kod) = await KayitAcAsync();

            var yanit = await DogrulaAsync(eposta, kod);
            yanit.StatusCode.Should().Be(HttpStatusCode.OK);

            // VAKUM KIRICI: 200 yetmez - DB'de gercekten degisti mi?
            using var scope = _factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var musteri = await db.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
            musteri.email_verified.Should().BeTrue("dogru kod hesabi dogrulamali");
            musteri.email_verification_token.Should().BeNull("kod TEK KULLANIMLIK - kullanildiktan sonra silinmeli");
        }

        // ── (2) BES YANLIS DENEME -> KILIT; sonrasinda DOGRU KOD BILE gecmez ──────────────
        [Fact]
        public async Task BES_YANLIS_DENEME_KILITLER_ve_DOGRU_KOD_BILE_GECMEZ()
        {
            if (Skipped()) return;
            var (eposta, kod) = await KayitAcAsync();

            for (var i = 1; i <= 5; i++)
            {
                var y = await DogrulaAsync(eposta, "000000");
                y.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{i}. yanlis deneme reddedilmeli");
            }

            // AYIRT EDICI: sinir gercekten ISLIYORSA artik DOGRU kod da kabul EDILMEMELI.
            // Bu olmadan test "yanlis kod 400 doner" demis olurdu - ki o zaten sayacsiz da dogru.
            var dogruDeneme = await DogrulaAsync(eposta, kod);
            dogruDeneme.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "bes hatali denemeden SONRA dogru kod bile gecmemeli - yoksa sinir ANLAMSIZ olur");

            var govde = await dogruDeneme.Content.ReadAsStringAsync();
            govde.Should().Contain("Çok fazla", "kullaniciya SEBEBI soylenmeli: yeni kod istemesi gerekiyor");

            using var scope = _factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var musteri = await db.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
            musteri.email_verified.Should().BeFalse("kilitliyken hesap dogrulanmamali");
        }

        // ── (3) SURESI DOLMUS KOD -> 400 ve AYRI mesaj ────────────────────────────────────
        [Fact]
        public async Task SURESI_DOLMUS_KOD_400_ve_AYRI_MESAJ()
        {
            // Gonderim zamani 11 dakika geriye alinir (omur 10 dk).
            var (eposta, kod) = await KayitAcAsync(gonderimZamani: DateTime.Now.AddMinutes(-11));

            var yanit = await DogrulaAsync(eposta, kod);
            yanit.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var govde = await yanit.Content.ReadAsStringAsync();
            // CIFT-ANLAM KIRICI: 400 iki sebepten gelebilir (yanlis kod / sure). Kullanicinin
            // YAPACAGI SEY farkli oldugu icin mesaj da ayrismali.
            govde.Should().Contain("süresi doldu", "suresi dolan kodda kullanici YENI KOD istemeli");
        }

        // ── (3b) DOGRULANMIS HESAP, KAYITSIZ ADRESTEN AYIRT EDILEMEZ ─────────────────────
        //
        // DALGA ICI DENETIM BULGUSU - KUSURU BU DALGA URETTI. Eski uc yalniz JETON aliyordu;
        // "zaten dogrulanmis" dalina ancak gecerli jeton tasiyan biri varabilirdi. LF-5 girdiyi
        // (e-posta + kod) yapinca o dal, TEK BASINA E-POSTA YAZAN birinin sorgulayabilecegi bir
        // VARLIK ORAKULU haline geldi: 200 "zaten dogrulanmis" <-> 400 "kod gecersiz".
        //
        // BU PIN AYIRT EDICIDIR: yalniz durum kodunu degil, IKI FARKLI ADRESIN YANITININ
        // BIRBIRINE ESIT oldugunu olcer. Kirmizi-once GORULDU - donusum yapilmadan once
        // dogrulanmis hesap 200 donuyordu ve pin TAM 1 ISIMLI KIRMIZI verdi.
        [Fact]
        public async Task DOGRULANMIS_HESAP_VARLIK_ORAKULU_DEGIL()
        {
            var (eposta, kod) = await KayitAcAsync();
            (await DogrulaAsync(eposta, kod)).StatusCode.Should().Be(HttpStatusCode.OK,
                "on kosul: hesap once GERCEKTEN dogrulanmali");

            // Ayni adres IKINCI kez - artik dogrulanmis bir hesap.
            var dogrulanmis = await DogrulaAsync(eposta, kod);
            var dogrulanmisGovde = await dogrulanmis.Content.ReadAsStringAsync();

            // Hic kayit olmamis bir adres - saldirganin kiyaslama tabani.
            var kayitsiz = await DogrulaAsync(YeniEposta().ToLowerInvariant(), kod);
            var kayitsizGovde = await kayitsiz.Content.ReadAsStringAsync();

            dogrulanmis.StatusCode.Should().Be(kayitsiz.StatusCode,
                "DURUM KODU iki adresi ayirt ederse adres varligi sizar");
            dogrulanmisGovde.Should().Be(kayitsizGovde,
                "GOVDE iki adresi ayirt ederse adres varligi sizar");

            // VAKUM KIRICI: ikisinin de BOS olup "esit" cikmasi ihtimalini eler.
            dogrulanmisGovde.Should().Contain("false", "yanit gercekten bir hata yaniti olmali");
        }

        // ── (4) DUZ KOD VERITABANINDA SAKLANMAZ ───────────────────────────────────────────
        [Fact]
        public async Task DUZ_KOD_VERITABANINDA_SAKLANMAZ()
        {
            if (Skipped()) return;

            // ══ BU PIN KENDI YAZDIGINI OLCMEZ - MK-6 ILE DUZELTILDI ═══════════════════════
            // ILK YAZIMDA `KayitAcAsync` cagriliyordu; o yardimci kayittan SONRA satiri
            // BILINEN kodun ozetiyle EZIYOR. Yani test, KAYIT YOLUNUN yazdigini degil
            // KENDI yazdigini dogruluyordu. MUT-16 (register'da `Ozetle(...)` kaldirilip DUZ
            // KOD yazildi) bu yuzden **0 KIRMIZI** verdi - pin KORDU.
            // Artik kayit ucu kosuluyor ve satira DOKUNULMADAN okunuyor.
            var eposta = YeniEposta();
            var anon = _factory!.CreateClient();
            var kayit = await anon.PostAsJsonAsync("/api/auth/register", new
            {
                name = "LF5 Ozet",
                email = eposta,
                phone = "5550000000",
                password = "GecerliParola1",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });
            kayit.StatusCode.Should().Be(HttpStatusCode.Created);

            using var scope = _factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var kucuk = eposta.ToLowerInvariant();
            var musteri = await db.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == kucuk);

            var saklanan = musteri.email_verification_token;
            saklanan.Should().NotBeNullOrWhiteSpace("kayit bir dogrulama degeri yazmali");

            // ASIL IDDIA: saklanan deger 6 HANELI BIR KOD OLAMAZ. Duz kod yazilsaydi bu
            // assert kirmizi olurdu - MUT-16'nin yakalanmasi tam olarak buna bagli.
            saklanan.Should().NotMatchRegex(@"^\d{6}$",
                "veritabaninda 6 haneli DUZ KOD durmamali - HMAC ozeti durmali");
            saklanan!.Length.Should().Be(64,
                "HMAC-SHA256 hex = 64 karakter; 6 haneli bir deger bu uzunlukta OLAMAZ");
            saklanan.Should().MatchRegex("^[0-9A-F]{64}$", "buyuk harf hex bekleniyor");
        }

        // ── (5) OZET BIBERLI: DUZ SHA-256 DEGIL ───────────────────────────────────────────
        // Bu, dalganin GUVENLIK cekirdegidir. Duz SHA-256 olsaydi DB sizan bir saldirgan
        // 10^6 ozeti onceden hesaplayip kodu SANIYEDE cozerdi. Biber sunucuda durdugu icin
        // sozluk ONCEDEN HESAPLANAMAZ. Pin: uretilen ozet, biberSIZ SHA-256'dan FARKLI olmali.
        [Fact]
        public async Task OZET_BIBERLI_DUZ_SHA256_DEGIL()
        {
            if (Skipped()) return;
            using var scope = _factory!.Services.CreateScope();
            var kodServisi = scope.ServiceProvider.GetRequiredService<IDogrulamaKoduServisi>();

            const string kod = "123456";
            var biberli = kodServisi.Ozetle(kod);

            var biberSiz = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(kod)));

            biberli.Should().NotBe(biberSiz,
                "ozet SUNUCU BIBERIYLE uretilmeli - duz SHA-256 olsaydi alti haneli kodun tum " +
                "uzayi (10^6) onceden hesaplanip DB sizintisinda aninda cozulurdu");

            // Ayni kod ayni ozeti vermeli (dogrulama calisabilsin).
            kodServisi.Ozetle(kod).Should().Be(biberli, "ozetleme DETERMINISTIK olmali");
            kodServisi.Eslesiyor(kod, biberli).Should().BeTrue("dogru kod eslesmeli");
            kodServisi.Eslesiyor("654321", biberli).Should().BeFalse("yanlis kod eslesmemeli");
        }

        // ── (6) URETILEN KOD BICIMI: 6 HANE, SAYISAL, BASTAKI SIFIR KORUNUR ───────────────
        [Fact]
        public void URETILEN_KOD_ALTI_HANELI_SAYISAL()
        {
            if (Skipped()) return;
            using var scope = _factory!.Services.CreateScope();
            var kodServisi = scope.ServiceProvider.GetRequiredService<IDogrulamaKoduServisi>();

            var kodlar = Enumerable.Range(0, 200).Select(_ => kodServisi.Uret()).ToList();

            kodlar.Should().OnlyContain(k => k.Length == 6, "kod TAM 6 hane olmali");
            kodlar.Should().OnlyContain(k => k.All(char.IsDigit), "kod SAYISAL olmali");
            // VAKUM KIRICI: sabit bir deger donduren bir uygulama da yukaridakileri gecerdi.
            kodlar.Distinct().Count().Should().BeGreaterThan(150,
                "kodlar RASTGELE olmali - 200 uretimde 150'den az benzersiz deger, ureteci supheli kilar");
        }

        // Kaynak-sozlesme pini icin depo koku. Sessiz skip YOK: kok bulunamazsa GURULTULU
        // duser - kaynagi okuyamayan bir pin yesil KALAMAZ.
        private static readonly Lazy<string> Lf5Kok = new(() =>
        {
            var d = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !System.IO.File.Exists(
                       System.IO.Path.Combine(d.FullName, "frontend", "index.html")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException("Depo koku bulunamadi (frontend/index.html).");
            return d.FullName;
        });

        private static string Lf5Oku(string goreliYol)
        {
            var tam = System.IO.Path.Combine(
                Lf5Kok.Value, goreliYol.Replace('/', System.IO.Path.DirectorySeparatorChar));
            System.IO.File.Exists(tam).Should().BeTrue($"pinlenen kaynak bulunmali: {goreliYol}");
            return System.IO.File.ReadAllText(tam);
        }

        // ── (8) SAYAC YAZAN ve OKUYAN AYNI PRIMITIFI KULLANIR ────────────────────────────
        //
        // ** KAYNAK-SOZLESME PINI ** (bu dosyadaki TEK istisna; digerleri davranis pinidir).
        // DAVRANIS KANITI NEREDE: bu rig'de Redis YOK (Docker yok) - kusur YALNIZ Redis'te
        // gorunur, bellek uygulamasinda GORUNMEZ. Davranis kaniti bu yuzden CANLI SUNUCUDA
        // alindi ve muhur 58 bolum 4.4'te ONCE/SONRA olarak yazili:
        //     ONCE  : istek 1 -> 400, istek 2 -> 500 (WRONGTYPE), istek 3 -> 500
        //     SONRA : istek 1..4 -> 400, istek 5 -> "cok deneme" 400   (500 YOK)
        //
        // NEDEN PIN GEREKLI: kusur DAGITIMDAN ONCE hicbir testte gorunmuyordu ve TAM BU
        // YUZDEN canliya cikti. Kaynak sozlesmesi, ayrismanin GERI GELMESINI engeller.
        [Fact]
        public void SAYAC_OKUMASI_ARTIRMAYLA_AYNI_PRIMITIFI_KULLANIR()
        {
            var authManager = Lf5Oku("Divisima.Bussiness/Concrete/AuthManager.cs");
            var redis = Lf5Oku("Divisima.Core/Utilities/Caching/RedisCacheService.cs");

            // (a) URETIM sayaci `SayacOkuAsync` ile okur - `GetAsync<long>` ile DEGIL.
            authManager.Should().Contain("_cache.SayacOkuAsync(anahtar)",
                "sayac okumasi artirmayla AYNI primitife bagli uye uzerinden yapilmali");
            authManager.Should().NotContain("_cache.GetAsync<long>(anahtar)",
                "CANLIDA KIRAN CAGRI BUYDU: Redis'te ham string anahtari IDistributedCache "
                + "hash'i gibi okumak WRONGTYPE firlatir");

            // (b) Redis uygulamasi sayaci HAM STRING olarak okur (`StringGetAsync`), yani
            //     `StringIncrementAsync`in YAZDIGI temsille AYNI dunyada.
            redis.Should().Contain("StringIncrementAsync", "sayac artirma ham string uzerinde");
            redis.Should().Contain("StringGetAsync", "sayac okuma da AYNI temsilde olmali");

            // AYIRT EDICILIK (MK-6 ruhu): `SayacOkuAsync` govdesi `_cache` (IDistributedCache)
            // KULLANMAMALI. Dosya genelinde `_cache` cokca geciyor - bu yuzden sayim DEGIL,
            // METODUN KENDI GOVDESI kesilip taranir; aksi halde assert BEDAVA dogru olurdu.
            //
            // PENCERE SABIT UZUNLUK OLAMAZ (ilk yazimda 400 karakter denendi ve pin YANLIS
            // KIRMIZI verdi): pencere metodun DISINA tasip bir SONRAKI metodun `_cache`
            // kullanimini yakaladi. Sinir, bir sonraki uye bildirimidir.
            var bas = redis.IndexOf("public async Task<long> SayacOkuAsync", StringComparison.Ordinal);
            bas.Should().BeGreaterThan(0, "vakum kirici: metot GERCEKTEN bulunmali");
            var son = redis.IndexOf("\n        public ", bas + 10, StringComparison.Ordinal);
            son.Should().BeGreaterThan(bas, "metodun bittigi yer bulunabilmeli");
            var govde = redis.Substring(bas, son - bas);
            govde.Should().Contain("StringGetAsync", "kesilen govde DOGRU metot olmali");
            govde.Should().NotContain("_cache.",
                "sayac okumasi IDistributedCache yolundan GECMEZ - ayrisma TAM ORADA dogar");
        }

        // ── (7) 60 SN SOGUMA: YANIT AYNI 200, AMA YENI KOD URETILMEZ ─────────────────────
        //
        // TARIFTEN BILINCLI SAPMA - MERKEZE RAPORLANIR. Tarif kabul olcutu olarak
        // "soguma 429" diyordu. 429 DONULMEDI: bu ucun TUM VARLIK NEDENI (G2b) adresin
        // kayitli olup olmadigini sizdirmamaktir; sogumada FARKLI bir durum kodu donmek
        // tam o sizintiyi GERI ACARDI - saldirgan iki kez ust uste isteyip 429 alirsa
        // "bu adres KAYITLI" bilgisini okur. Soguma bu yuzden ISTEMCIDE gorunur
        // (geri sayim), SUNUCUDA sessizdir.
        //
        // BU YUZDEN PIN DURUM KODUNU DEGIL YAN ETKIYI OLCER - ki asil sart odur:
        // ikinci istek YENI KOD URETMEMELI ve gonderim zamanini ILERLETMEMELIDIR.
        // Yalniz "200 dondu" demek CIFT-ANLAMLI olurdu: soguma calissa da calismasa da 200.
        [Fact]
        public async Task SOGUMA_YENI_KOD_URETMEZ_ama_YANIT_AYNI_200()
        {
            if (Skipped()) return;
            var (eposta, _) = await KayitAcAsync();
            var anon = _factory!.CreateClient();
            var yol = "/api/auth/resend-verification?email=" + Uri.EscapeDataString(eposta);

            var ilk = await anon.PostAsync(yol, null);
            ilk.StatusCode.Should().Be(HttpStatusCode.OK, "on kosul: ilk istek gecmeli");

            string ozetIlk; DateTime? zamanIlk;
            using (var s = _factory!.Services.CreateScope())
            {
                var db = s.ServiceProvider.GetRequiredService<DivisimaDbContext>();
                var m = await db.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
                ozetIlk = m.email_verification_token!;
                zamanIlk = m.email_verification_sent_at;
            }
            ozetIlk.Should().NotBeNullOrWhiteSpace("VAKUM KIRICI: ilk istek GERCEKTEN kod uretmis olmali");

            // Hemen ikinci istek - soguma penceresi (60 sn) ICINDE.
            var ikinci = await anon.PostAsync(yol, null);
            ikinci.StatusCode.Should().Be(HttpStatusCode.OK,
                "SIZINTI SINIRI: soguma FARKLI bir durum kodu donmez - 429 adresin kayitli oldugunu ele verirdi");

            using (var s = _factory!.Services.CreateScope())
            {
                var db = s.ServiceProvider.GetRequiredService<DivisimaDbContext>();
                var m = await db.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
                m.email_verification_token.Should().Be(ozetIlk,
                    "soguma icindeki istek YENI KOD URETMEMELI - aksi halde 60 sn siniri ANLAMSIZ");
                m.email_verification_sent_at.Should().Be(zamanIlk,
                    "gonderim zamani ILERLEMEMELI - ilerleseydi soguma penceresi her istekte YENIDEN baslar ve HIC dolmazdi");
            }
        }
    }
}
