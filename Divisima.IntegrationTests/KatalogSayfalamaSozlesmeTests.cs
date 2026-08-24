using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // === D3 - ISTEMCI SAYFALAMA SOZLESMESI ==============================================
    //
    // OLCULEN ZARAR (403 urunluk katalogla, gercek tarayicida):
    //   ilk yukleme 2 API istegi, bellege giren urun 24   <- VERITABANINDA 403
    //   kategori rotalari 0 EK ISTEK; 8 kategorinin her birinde 3 urun gorunuyor (DB'de ~50)
    // Kok sebep: `loadCatalog` HER ZAMAN { page:1, size:24 } cekiyor, sayfa 2'yi HIC istemiyor
    // ve `replaceProducts` bellegi o 24 urunle DEGISTIRIYORDU. Musteri katalogun ilk 24
    // urununu gezebiliyor, kalan %94'e GEZINEREK ULASAMIYORDU (tek kacis arama).
    // 3 urunluk gelistirme verisinde GORUNMEZDI - D3'un varlik sebebi tam olarak budur.
    //
    // BU SINIF VERITABANI ACMAZ - bilincli. Depoda 46 test sinifi kendi veritabanini kuruyor
    // ve SQL Server bunlari `model` uzerinden serilestiriyor; 47. katilimci eklendiginde bes
    // AYRI sinif "Could not obtain exclusive lock on database 'model'" ile dustu (CI kirmizisi
    // 10d794d). Bu pinler KAYNAK METNINI okur, veritabanina ihtiyaclari YOKTUR.
    //
    // PIN SINIRI (durust kayit - Dalga 4 / Dalga A ile AYNI): depoda JS/DOM kosucusu YOK,
    // bu yuzden burada TARAYICI DAVRANISI degil KAYNAK SOZLESMESI tutuluyor. Davranis kaniti
    // CLAUDE.md'nin D3 bolumundeki tarayici olcumlerindedir (403/403 urune ulasildi, 17 filter
    // istegi; kategori rotasi 1 istek atti; geri donusta liste sifirlanmadi).
    // SUNUCU tarafinin davranis pinleri AYRI ve GERCEK: StorefrontCatalogContractTests
    // (`Filter_IKINCI_SAYFA_...`, `Filter_KATEGORI_FILTRESINI_SUNUCUDA_Uygular`,
    //  `Filter_ZENGINLESTIRME_SAYFA_2_DE_...`).
    public class KatalogSayfalamaSozlesmeTests
    {
        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "frontend", "index.html")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: frontend/index.html iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pinler kaynagi okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen kaynak dosya bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        private static int Say(string metin, string desen)
        {
            var n = 0;
            for (var i = metin.IndexOf(desen, StringComparison.Ordinal); i >= 0;
                     i = metin.IndexOf(desen, i + desen.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        // ── 1) ISTEMCI IKINCI SAYFAYI GERCEKTEN ISTER ───────────────────────────────────
        [Fact]
        public void ISTEMCI_IKINCI_SAYFAYI_GERCEKTEN_ISTER()
        {
            var js = Oku("frontend/api-bridge.js");

            // VAKUM KIRICI: katalog ucu HALA cagriliyor olmali - yoksa "sayfa 2 isteniyor"
            // iddiasi, hicbir istek atilmadigi icin de "dogru" gorunurdu.
            Say(js, "api.products.filter(").Should().BeGreaterThan(1,
                "katalog ucu birden fazla yerden cagrilmali: ilk sayfa + sonraki sayfa");

            js.Should().Contain("sonrakiSayfayiCek",
                "sonraki sayfayi ceken bir yol BULUNMALI");
            js.Should().Contain("page: istenen",
                "istenen sayfa HESAPLANMALI - sabit 'page: 1' ile sayfa 2 asla istenemez");
            js.Should().Contain("d.sayfa + 1",
                "sonraki sayfa, kaydedilen sayfa durumundan TUREMELI");

            // Sunucunun bildirdigi toplam sayfa OKUNMALI, yoksa "daha var mi" bilinemez.
            js.Should().Contain("total_pages",
                "toplam sayfa sunucudan okunmali - istemci kendi tahminini yurutemez");

            // Dugme GERCEKTEN bu yola bagli olmali (sadece fonksiyonun var olmasi yetmez).
            js.Should().Contain("loadMoreApiBtn",
                "\"Daha Fazla Yukle\" dugmesi API sayfasini ceken yola BAGLI olmali");
        }

        // ── 2) SAYFALAR BIRIKIR - BELLEK EZILMEZ ────────────────────────────────────────
        [Fact]
        public void ISTEMCI_SAYFALARI_BIRIKTIRIR_BELLEGI_EZMEZ()
        {
            var js = Oku("frontend/api-bridge.js");

            js.Should().Contain("function appendProducts",
                "sayfalar EKLENEREK birikmeli - her sayfa bellegi bastan yazamaz");
            js.Should().Contain("varOlan[p.id]",
                "birikim KIMLIGE gore tekillestirilmeli - ayni urun iki kez girmemeli");

            // CIFT-ANLAM KIRICI: `replaceProducts` HALA var olmali (ilk yukleme mock'u
            // temizlemek icin onu kullanir) ama sonraki sayfa yolu ONU KULLANMAMALI.
            js.Should().Contain("function replaceProducts",
                "ilk yukleme mock katalogu TEMIZLEMEK icin replaceProducts'i korumali");

            var sonraki = js.IndexOf("async function sonrakiSayfayiCek", StringComparison.Ordinal);
            sonraki.Should().BeGreaterThan(0, "sonraki sayfa fonksiyonu bulunmali");
            var son = js.IndexOf("window.divisimaSonrakiSayfa", StringComparison.Ordinal);
            son.Should().BeGreaterThan(sonraki, "fonksiyon govdesi sinirlanabilmeli");
            var govde = js.Substring(sonraki, son - sonraki);
            govde.Should().NotContain("replaceProducts",
                "sonraki sayfa yolu bellegi EZMEMELI - kullanici geri dondugunde liste sifirlanmamali");
            govde.Should().Contain("appendProducts",
                "sonraki sayfa yolu urunleri EKLEMELI");
        }

        // ── 3) KATEGORI ROTASI SUNUCUYA category_id GONDERIR ────────────────────────────
        [Fact]
        public void KATEGORI_ROTASI_SUNUCUYA_KATEGORI_KIMLIGI_Gonderir()
        {
            var js = Oku("frontend/api-bridge.js");

            js.Should().Contain("function aktifKategoriId",
                "aktif kategori rotasinin GERCEK veritabani kimligi cozulebilmeli");
            js.Should().Contain("divisimaCategoryIdBySlug",
                "kimlik, kategori ucundan gelen slug->id haritasindan TUREMELI - uydurulmamali");
            js.Should().Contain("category_id: kategoriId",
                "kategori filtresi SUNUCUYA gonderilmeli");
            js.Should().Contain("kategoriSayfasiniHazirla",
                "kategori rotasina girildiginde o kategorinin sayfasi cekilmeli");

            // CIFT-ANLAM KIRICI: karsiligi OLMAYAN rota icin uydurma kimlik GONDERILMEMELI.
            js.Should().Contain("return 0",
                "veritabaninda karsiligi olmayan rota icin kimlik 0 (tum katalog) olmali");
        }

        // ── 4) URUN KATEGORI SLUGU VERITABANI SLUGUNDAN TURER ───────────────────────────
        // OLCULDU: `slugify(category_name)` ile DB slug'i AYRISIYORDU
        // ("D3OLCEK Kategori 1" -> "d3olcek-kategori-1"  vs  DB slug "d3olcek-1").
        // Sonucu: kategori rotasi urunleri suzemiyor VE etiket aramasi ISKALIYOR
        // (E1'de bir kez duzeltilen "cat_e4a-kategori" ham anahtar basimi geri geliyordu).
        [Fact]
        public void URUN_KATEGORI_SLUGU_VERITABANI_SLUGUNDAN_Turer()
        {
            var js = Oku("frontend/api-bridge.js");

            var bas = js.IndexOf("function categorySlugOf", StringComparison.Ordinal);
            bas.Should().BeGreaterThan(0, "categorySlugOf bulunmali");
            var son = js.IndexOf("function mapProduct", StringComparison.Ordinal);
            son.Should().BeGreaterThan(bas, "govde sinirlanabilmeli");
            var govde = js.Substring(bas, son - bas);

            govde.Should().Contain("c.slug",
                "kategori slug'i VERITABANI satirindan alinmali - tek dogruluk kaynagi odur");

            // CIFT-ANLAM KIRICI: yedek yol KALMALI (kategori cozulemezse ad slugify edilir),
            // ama VERITABANI SLUG'I ONCE denenmeli.
            govde.Should().Contain("slugify(",
                "kategori satiri bulunamazsa ad uzerinden yedek yol KORUNMALI");
            govde.IndexOf("c.slug", StringComparison.Ordinal)
                 .Should().BeLessThan(govde.IndexOf("p.category_name", StringComparison.Ordinal),
                "veritabani slug'i, ad tabanli yedekten ONCE denenmeli");

            // Etiket kaydi da AYNI anahtari kullanmali - iki taraf ayrisirsa ham anahtar basilir.
            var etiket = js.Substring(js.IndexOf("function registerCategoryLabels", StringComparison.Ordinal), 500);
            etiket.Should().Contain("c.slug",
                "etiket anahtari ile urun kategori slug'i AYNI kaynaktan turemeli");
        }

        // ══ TAKSONOMI: MENU VERITABANINDAN URETILIR ════════════════════════════════════
        //
        // OLCULEN ZARAR (D3, 403 urunluk katalogla): index.html'in kategori menusu SABIT bir
        // diziydi (`NAV` = yeni/elbise/ust/alt/dis/aksesuar/indirim) ve veritabaniyla yalnizca
        // "elbise" uzerinden kesisiyordu. Sonuclari: (a) DB'de VAR olan ama navda olmayan
        // kategoriye ROTA YOKTU - `#/kategori/d3olcek-3` SESSIZCE `#/kategori/tumu`ya yeniden
        // yaziliyordu; (b) navda VAR ama DB'de OLMAYAN kategori "gecerli" sayilip BOS sayfa
        // ciziyordu. Gercek katalog aktarildiginda (a) HER kategori icin gecerli olacakti.

        // ── 5) MENU SUNUCUDAN GELIR ─────────────────────────────────────────────────────
        [Fact]
        public void MENU_VERITABANINDAN_URETILIR_SABIT_TAKSONOMI_KULLANILMAZ()
        {
            var js = Oku("frontend/api-bridge.js");

            js.Should().Contain("function menuyuVeritabanindanKur",
                "menu, kategori ucunun yanitindan URETILMELI");
            js.Should().Contain("window.NAV = yeniNav",
                "index.html'in SABIT NAV dizisi DEGISTIRILMELI - uzerine eklemek eski slug'lari birakirdi");
            js.Should().Contain("window.CAT_INFO = yeniInfo",
                "CAT_INFO da yeniden kurulmali: index.html'deki sabit girdiler (ust/alt/aksesuar) "
              + "veritabaninda karsiligi olmasa bile rotayi 'gecerli' yapiyordu");
            js.Should().Contain("window.MAINS = ",
                "filtre/pill listesi de ayni kaynaktan gelmeli");

            foreach (var ciz in new[] { "renderNav", "renderMob", "renderPills" })
                js.Should().Contain(ciz, $"menu yeniden kurulunca {ciz} tekrar cizilmeli");

            // ILK YUKLEME MALIYETI ARTMAMALI: kategori ucu ZATEN cagriliyor; menu AYNI
            // yanittan uretiliyor. Ikinci bir cagri eklenirse bu assert kirilir.
            Say(js, "api.categories.list(").Should().Be(1,
                "kategori ucu TEK KEZ cagrilmali - menu icin AYRI bir istek eklenemez");

            // TANIMLI OLMAK YETMEZ, CAGRILMALI DA.
            // Bu assert 5. kontrolde ACILAN BIR BOSLUKTAN sonra eklendi: `init` icindeki
            // cagriyi kaldiran bir mutasyon, fonksiyonun GOVDESI dosyada durdugu icin
            // digerlerinin hicbirini kirmiyordu - yani menu sabit taksonomiye geri doner
            // ve pinler YESIL kalirdi. Tanim + cagri = en az iki gecis.
            foreach (var fn in new[] { "menuyuVeritabanindanKur", "taksonomiRotasiniBagla", "kategoriRotasiniTazele" })
                Say(js, fn).Should().BeGreaterThan(1,
                    $"{fn} yalniz TANIMLI degil, acilis akisinda CAGRILMIS da olmali");
        }

        // ── 6) TANINMAYAN ROTA SESSIZCE YENIDEN YAZILMAZ ────────────────────────────────
        [Fact]
        public void TANINMAYAN_ROTA_SESSIZCE_YENIDEN_YAZILMAZ_404E_DUSER()
        {
            var js = Oku("frontend/api-bridge.js");

            js.Should().Contain("function taksonomiRotasiniBagla",
                "rota dogrulamasi baglanmali");
            js.Should().Contain("window.show404()",
                "taninmayan kategori rotasi uygulamanin KENDI 404'une dusmeli - "
              + "sessizce 'tumu'ya cevrilmemeli");

            // CIFT-ANLAM KIRICI: "her seyi 404'e dusur" YANLIS duzeltmedir. Sentetik
            // gorunumler (tumu/yeni/indirim) VERITABANI KATEGORISI DEGILDIR ama GECERLIDIR.
            js.Should().Contain("SENTETIK_ROTALAR",
                "sentetik gorunumler gecerli sayilmali - aksi halde vitrinin ana sayfalari 404 olurdu");
            js.Should().Contain("SENTETIK_ROTALAR.indexOf(cat) >= 0",
                "gecerlilik kontrolu sentetik gorunumleri KAPSAMALI");

            // ILK YUKLEME YARISI: `defer` yuzunden index.html'in router'i ONCE kosuyor ve
            // adresi yeniden yaziyor; asil istenen slug gezinme kaydindan okunmali.
            js.Should().Contain("ILK_KATEGORI_SLUG",
                "dogrudan acilan taninmayan rota da 404'e dusmeli - ilk yukleme yarisi kapatilmali");
            js.Should().Contain("getEntriesByType(\"navigation\")",
                "ISTENEN slug, hash yeniden yazildiktan sonra ancak gezinme kaydindan okunabilir");
        }

        // ── 7) KATEGORI YOKSA MENU BOS GORUNMEZ ─────────────────────────────────────────
        // Yedek, UYDURMA bir kategori listesi DEGIL: "tumu/yeni/indirim" bellekteki urunler
        // uzerinden turetilen istemci tarafi gorunumlerdir, kategori tablosuna BAGLI DEGILDIR.
        [Fact]
        public void KATEGORI_YOKSA_MENU_BOS_GORUNMEZ()
        {
            var js = Oku("frontend/api-bridge.js");

            js.Should().Contain("{ slug: \"yeni\"",
                "kategori gelmese de 'yeni' gorunumu menude kalmali");
            js.Should().Contain("{ slug: \"indirim\"",
                "kategori gelmese de 'indirim' gorunumu menude kalmali");
            js.Should().Contain("[\"tumu\", \"Tümü\"]",
                "filtre listesi her zaman 'tumu' ile baslamali");

            // 404 sayfasinin "populer kategoriler" satiri da SABIT slug'lar tasiyordu; kategori
            // yokken o satir OLU BAGLANTI listesine donusuyordu (404 -> yine 404). Olculdu.
            js.Should().Contain("{ slug: \"tumu\" }, { slug: \"yeni\" }, { slug: \"indirim\" }",
                "404 sayfasinin kategori satiri, kategori yokken HER ZAMAN GECERLI baglantilara dusmeli");
        }

        // ── 8) ALT KATEGORILER SUNUCUDAN GELIR - UYDURULMAZ ─────────────────────────────
        // OLCULDU: `CategoryResponseDto` ZATEN `sub_categories` tasiyor ve `CategoryManager`
        // onu dolduruyor; uc bugun `[]` donuyor (tablo bos, ayri uc yok). Yani sozlesme
        // MEVCUT - dolu geldigi gun alt menu kendiliginden cizilir.
        [Fact]
        public void ALT_KATEGORILER_SUNUCUDAN_GELIR_UYDURULMAZ()
        {
            var js = Oku("frontend/api-bridge.js");

            js.Should().Contain("c.sub_categories",
                "alt menu SUNUCUDAN gelen sub_categories'ten uretilmeli");
            js.Should().Contain("if (alt.length)",
                "alt menu YALNIZCA gercekten alt kategori varsa cizilmeli - bos dizi menu uretmemeli");

            // CIFT-ANLAM KIRICI: index.html'in SABIT alt menu listesi (gunluk/abiye/bluz/...)
            // api-bridge tarafina KOPYALANMAMIS olmali - kaynak tek olmali.
            foreach (var uydurma in new[] { "\"gunluk\"", "\"abiye\"", "\"bluz\"", "\"trenckot\"" })
                js.Should().NotContain(uydurma,
                    "sabit alt kategori slug'lari istemciye KOPYALANMAMALI - kaynak veritabanidir");
        }
    }
}
