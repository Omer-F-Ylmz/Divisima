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
    }
}
