using System.Linq.Expressions;
using Divisima.API.Controllers;
using Divisima.Core.DataAccess;
using Divisima.Core.Entities.Abstract;
using Divisima.Core.Utilities.Dtos;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ LAUNCH-DEPLOY-1 / LF-2 - ALAN ADI GECISI + SITEMAP KOKU ════════════════════════════
    //
    // IKI AYRI SOZLESME PINLENIR:
    //
    //   (1) ALAN ADI TEK BICIMLI. Depo `divisima.net`e tasindi. Eski ad YALNIZ tarihsel
    //       muhurlerde (`docs/muhur/`) yasar - orasi MK-11/d geregi BAYT-SABITTIR, cunku bir
    //       muhur "o gun ne olculdu"yu tasir; gecmisi yeniden yazmak kanit degerini yok eder.
    //       Calisan HER artefaktta (kod · yapilandirma · vitrin · ops · is akisi) sayim 0.
    //
    //   (2) SITEMAP KOKU ISTEMCIDEN GELMEZ. Bu, LD-1'de olculen canli bir kusurun kapanisi
    //       (AV-3 / SD-4, GF-7'den one cekildi). Ayrintili gerekce `SeoController`in kendi
    //       basinda; ozeti: uc `[AllowAnonymous]` idi ve site koku bir SORGU PARAMETRESIYDI,
    //       yani arama motoruna sunulan belgenin govdesini herhangi bir anonim cagiran
    //       belirleyebiliyordu - ustelik deger kacislanmadan XML'e yaziliyordu.
    //
    // PIN TURLERI ACIKCA ISARETLI: (1) kaynak-sozlesme pinidir (artefakt metni olcer).
    // (2) DAVRANIS pinidir - gercek controller ornegi kosturulur, uretilen XML govdesi
    // okunur. Veritabani GEREKMEZ: DAL'lar elle yazilmis sahtelerle beslenir (yeni bir test
    // bagimliligi EKLENMEDI - `00a:180` launch oncesi yeni paket YASAK).
    public class LaunchFix2SozlesmeTests
    {
        private static readonly Lazy<string> Kok = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: docker-compose.yml iceren ust dizin yok. Sessiz skip YOK.");
            return d.FullName;
        });

        // ══════════════════════ (1) ALAN ADI GECISI ════════════════════════════════════════

        // CAPA PARCALI KURULUYOR - bu dosyanin KENDISI taramaya giriyor. Literal olarak
        // yazilsaydi pin KENDI METNIYLE kirmizi olurdu ve "dosyayi haric tut" telafisi
        // taramada bir KOR NOKTA acardi. Parcali kurulum bu boslugu HIC ACMAZ.
        private const string EskiAd = "divisima" + ".com";
        private const string YeniAd = "divisima" + ".net";

        // ══ TARAMA EVRENI = DEPONUN IZLEDIGI DOSYALAR (`git ls-files`) ═════════════════════
        //
        // ILK YAZIMDA dosya sistemi gezilerek taraniyordu ve pin YERELDE KIRMIZI, CI'DA YESIL
        // olurdu - yani sonuc MAKINEYE bagliydi. OLCULDU (bu pin yazilirken ORTAYA CIKTI):
        // gezinti DORT dosya yakaladi - `Divisima.API/appsettings.Development.json` ve UC adet
        // `Divisima.API/logs/*.log`. Dordu de `git ls-files --error-unmatch` ile IZLENMIYOR ve
        // `git check-ignore` ile YOK SAYILIYOR cikti; temiz bir CI checkout'unda HIC YOKLAR.
        // Gelistiricinin yerel ayar dosyasi ve calisma zamani loglari DEPONUN ICERIGI DEGILDIR.
        //
        // Sabit bir "sunlari atla" listesi de yazilabilirdi ama o liste, her yeni izlenmeyen
        // dosya turunde SESSIZCE bir kor nokta acardi (LF-1/B-7'de tam bu yasandi). `git`in
        // kendi cevabi hem TEK KAYNAK hem de kendini gunceller.
        private static IReadOnlyList<string> IzlenenDosyalar => _izlenen.Value;

        private static readonly Lazy<IReadOnlyList<string>> _izlenen = new(() =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "ls-files -z")
            {
                WorkingDirectory = Kok.Value,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var p = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("git baslatilamadi - SESSIZ SKIP YOK.");
            var cikti = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30_000);

            if (p.ExitCode != 0)
                throw new InvalidOperationException(
                    $"`git ls-files` exit {p.ExitCode} - depo evreni okunamadi. Pin OLCEMEDIGINI " +
                    "sessizce yesil gecemez.");

            // `-z` NUL ayirici: bosluk/Unicode iceren yollar bolunmez.
            return cikti.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        });

        private static bool MuhurMu(string goreliYol) =>
            goreliYol.StartsWith("docs/muhur/", StringComparison.Ordinal);

        private static int GecisSayisi(string goreliYol, string capa)
        {
            string metin;
            try { metin = File.ReadAllText(Path.Combine(Kok.Value, goreliYol)); }
            catch (IOException) { return 0; }          // kilitli/gecici dosya
            catch (UnauthorizedAccessException) { return 0; }

            var n = 0;
            for (var i = metin.IndexOf(capa, StringComparison.OrdinalIgnoreCase);
                 i >= 0;
                 i = metin.IndexOf(capa, i + 1, StringComparison.OrdinalIgnoreCase))
                n++;
            return n;
        }

        [Fact]
        public void ESKI_ALAN_ADI_YALNIZ_TARIHSEL_MUHURLERDE_KALIR()
        {
            var kirliler = IzlenenDosyalar
                .Where(y => !MuhurMu(y))
                .Select(y => (Yol: y, Sayi: GecisSayisi(y, EskiAd)))
                .Where(x => x.Sayi > 0)
                .ToList();

            kirliler.Should().BeEmpty(
                "calisan her artefakt divisima.net'e tasindi; eski ad yalnizca docs/muhur/ " +
                "altinda (BAYT-SABIT tarihsel kayit) kalabilir. Kirli dosyalar: " +
                string.Join(", ", kirliler.Select(x => $"{x.Yol}({x.Sayi})")));
        }

        // VAKUM KIRICI: yukaridaki assert, tarayici HICBIR DOSYA gormese de yesil kalirdi.
        // Bu test tarayicinin gercekten calistigini BILINEN-POZITIFLE gosterir ve ayni
        // zamanda muhurlerin DOKUNULMAMIS oldugunu pinler.
        [Fact]
        public void TARAYICI_CALISIYOR_MUHURLERDE_ESKI_AD_HALA_VAR()
        {
            var taranan = IzlenenDosyalar.Count;
            taranan.Should().BeGreaterThan(200,
                "bilinen-pozitif: depoda yuzlerce IZLENEN dosya var; dusuk bir sayi " +
                "tarayicinin yanlis kokte kostugunu gosterir");

            // EVRENIN KENDISI POZ/NEG SINANIR: izlenen bir dosya ICERDE, yok sayilan bir dosya
            // DISARIDA olmali. Bu olmadan "0 kirli bulundu" sonucu, evren BOS oldugu icin de
            // dogru cikabilirdi - yani asil assert VAKUMDA yesil kalirdi.
            IzlenenDosyalar.Should().Contain("docker-compose.yml",
                "bilinen-pozitif: bu dosya depoda izleniyor");
            IzlenenDosyalar.Should().NotContain(
                y => y.EndsWith("appsettings.Development.json", StringComparison.Ordinal),
                "bilinen-negatif: gelistiricinin yerel ayar dosyasi .gitignore'dadir ve depo " +
                "evrenine GIRMEZ - girseydi pin MAKINEYE BAGLI hale gelirdi (yerelde kirmizi, " +
                "CI'da yesil; bu pin yazilirken birebir olculdu)");
            IzlenenDosyalar.Should().NotContain(y => y.Contains("/logs/", StringComparison.Ordinal),
                "bilinen-negatif: calisma zamani loglari depo icerigi DEGILDIR");

            var muhurdekiEski = IzlenenDosyalar.Where(MuhurMu).Sum(y => GecisSayisi(y, EskiAd));
            muhurdekiEski.Should().BeGreaterThan(0,
                "muhurler tarihsel kayittir ve eski adi TASIMAYA DEVAM ETMELI - bu sayi 0'a " +
                "duserse biri gecmisi yeniden yazmis demektir (MK-11/d ihlali)");

            var yeniAdSayisi = IzlenenDosyalar.Where(y => !MuhurMu(y)).Sum(y => GecisSayisi(y, YeniAd));
            yeniAdSayisi.Should().BeGreaterThan(0, "vakum kirici: gecis GERCEKTEN yapilmis olmali");
        }

        // ══════════════════════ (2) SITEMAP KOKU - DAVRANIS ════════════════════════════════

        private const string Kok1 = "https://divisima.net";
        private const string Kok2 = "https://baska-vitrin.example";

        private static SeoController Kur(string? storefrontBaseUrl)
        {
            var ayarlar = new Dictionary<string, string?>();
            if (storefrontBaseUrl != null) ayarlar["Storefront:BaseUrl"] = storefrontBaseUrl;
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(ayarlar).Build();

            return new SeoController(
                new SahteUrunDal(new List<Product> { new() { id = 7 } }),
                new SahteKategoriDal(new List<Category> { new() { id = 3 } }),
                cfg);
        }

        private static async Task<string> GovdeAsync(SeoController c)
        {
            var sonuc = await c.Sitemap();
            return sonuc.Should().BeOfType<ContentResult>().Subject.Content ?? "";
        }

        // ── AYIRT EDICI DENEY: ayni cagri, IKI FARKLI yapilandirma -> IKI FARKLI govde.
        // Tek bir kokle olculseydi, kok SABIT yazilmis olsa da pin yesil kalirdi.
        [Fact]
        public async Task Sitemap_KOKU_YAPILANDIRMADAN_OKUR_SABIT_DEGIL()
        {
            var a = await GovdeAsync(Kur(Kok1));
            var b = await GovdeAsync(Kur(Kok2));

            a.Should().Contain($"<loc>{Kok1}/</loc>");
            a.Should().NotContain(Kok2);
            b.Should().Contain($"<loc>{Kok2}/</loc>");
            b.Should().NotContain(Kok1, "kok SABIT olsaydi ikinci govde de birinciyi tasirdi");

            // Vakum kirici: govde gercekten sitemap ve kalemleri iceriyor.
            a.Should().Contain("<urlset").And.Contain("/#/urun/7").And.Contain("/#/kategori/3");
        }

        [Fact]
        public async Task Sitemap_STOREFRONT_BASEURL_BOSSA_GURULTULU_DUSER()
        {
            foreach (var bos in new[] { (string?)null, "", "   " })
            {
                var sonuc = await Kur(bos).Sitemap();
                var kod = sonuc.Should().BeAssignableTo<ObjectResult>().Subject.StatusCode;
                kod.Should().Be(500,
                    $"ayar '{bos ?? "(yok)"}' iken sessizce bir varsayilana KACILMAMALI - yanlis " +
                    "alan adli bir sitemap, hic sitemap olmamasindan daha zararlidir");
            }
        }

        [Fact]
        public async Task Sitemap_KOK_XML_KACISLANIR()
        {
            var govde = await GovdeAsync(Kur("https://divisima.net/?iz=a&b=c"));

            govde.Should().Contain("&amp;", "ham '&' XML'i BOZAR");
            govde.Should().NotContain("=a&b", "kacislanmamis '&' govdede KALMAMALI");
        }

        // ── IMZA PINI: davranisin ONKOSULU. Kaynak metnini DEGIL, DERLENMIS imzayi olcer -
        // yani "yorumda yaziyor ama kodda duruyor" tuzagina bagisiktir (MK-8 eki).
        [Fact]
        public void Sitemap_HICBIR_SORGU_PARAMETRESI_KABUL_ETMEZ()
        {
            var m = typeof(SeoController).GetMethod(nameof(SeoController.Sitemap));
            m.Should().NotBeNull("bilinen-pozitif: metot var");

            m!.GetParameters().Should().BeEmpty(
                "site koku TEK KAYNAKTAN (Storefront:BaseUrl) gelir; opsiyonel bir parametre " +
                "birakmak, kapatilan kapiyi ACIK tutardi - anonim cagiran sitemap govdesini " +
                "yeniden belirleyebilirdi");
        }

        // ── nginx tarafi: maskeleyen ek GERI GELMEMELI.
        [Fact]
        public void Nginx_SITEMAP_PROXYSI_SORGU_EKI_TASIMAZ()
        {
            var conf = File.ReadAllText(Path.Combine(Kok.Value, "ops", "infra", "nginx.conf"));
            var satir = conf.Split('\n')
                .FirstOrDefault(s => s.Contains("proxy_pass", StringComparison.Ordinal)
                                     && s.Contains("/api/seo/sitemap", StringComparison.Ordinal));

            satir.Should().NotBeNull("bilinen-pozitif: sitemap proxy satiri dosyada bulunmali");
            satir!.Should().NotContain("baseUrl",
                "kok nginx'ten VERILIRSE uygulamadaki kaynak yeniden MASKELENIR ve kusur " +
                "yalnizca proxy'yi atlayan cagrilarda gorunur hale gelir - LD-1'de tam bu yasandi");
        }

        // ══════════════════════ SAHTE DAL'LAR ══════════════════════════════════════════════
        //
        // `GetListAsync` DISINDA hicbir uye cagrilmaz; cagrilirsa test GURULTULU duser
        // (sessiz bir varsayilan donmek, pinin neyi olctugunu belirsizlestirirdi).
        private abstract class SahteDal<T> : IEntityRepository<T> where T : class, IEntity, new()
        {
            private readonly List<T> _kayitlar;
            protected SahteDal(List<T> kayitlar) => _kayitlar = kayitlar;

            public Task<List<T>> GetListAsync(Expression<Func<T, bool>> filter = null!) =>
                Task.FromResult(_kayitlar);

            private static Exception Kullanilmaz([System.Runtime.CompilerServices.CallerMemberName] string u = "") =>
                new NotSupportedException($"SahteDal.{u} bu pinde KULLANILMAZ - cagrildiysa pin yanlis sey olcuyor.");

            public T Get(Expression<Func<T, bool>> filter) => throw Kullanilmaz();
            public List<T> GetList(Expression<Func<T, bool>> filter = null!) => throw Kullanilmaz();
            public void Add(T entity) => throw Kullanilmaz();
            public void Update(T entity) => throw Kullanilmaz();
            public void Delete(T entity) => throw Kullanilmaz();
            public Task<T> GetAsync(Expression<Func<T, bool>> filter) => throw Kullanilmaz();
            public Task<List<T>> GetListNoTrackingAsync(Expression<Func<T, bool>> filter = null!) => throw Kullanilmaz();
            public Task<PagedResult<T>> GetPagedAsync(PagingRequestDto paging,
                Expression<Func<T, bool>> filter = null!, Expression<Func<T, object>> orderBy = null!,
                bool descending = false) => throw Kullanilmaz();
            public Task<int> CountAsync(Expression<Func<T, bool>> filter = null!) => throw Kullanilmaz();
            public Task<bool> AnyAsync(Expression<Func<T, bool>> filter = null!) => throw Kullanilmaz();
            public Task AddAsync(T entity) => throw Kullanilmaz();
            public Task UpdateAsync(T entity) => throw Kullanilmaz();
            public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate) => throw Kullanilmaz();
            public Task DeleteAsync(T entity) => throw Kullanilmaz();
            public Task<T> GetIgnoringFiltersAsync(Expression<Func<T, bool>> filter) => throw Kullanilmaz();
            public Task<List<T>> GetListIgnoringFiltersAsync(Expression<Func<T, bool>> filter = null!) => throw Kullanilmaz();
        }

        private sealed class SahteUrunDal : SahteDal<Product>, IProductDal
        {
            public SahteUrunDal(List<Product> kayitlar) : base(kayitlar) { }

            public Task<(List<Product> items, int totalCount)> GetListWithFilterAsync(
                int? categoryId, int? subCategoryId, List<string> sizes, List<string> colors,
                decimal? minPrice, decimal? maxPrice, bool? onSale, bool? inStock,
                string sort, int page, int size) =>
                throw new NotSupportedException("bu pinde KULLANILMAZ");
        }

        private sealed class SahteKategoriDal : SahteDal<Category>, ICategoryDal
        {
            public SahteKategoriDal(List<Category> kayitlar) : base(kayitlar) { }
        }
    }
}
