using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GF-2b - ISTEMCI OTURUM / SERVICE WORKER / CSP SOZLESME PINLERI ══════════════════
    //
    // Bu dosya GUVENLIK-FIX-2b (Eylul 2026) dalgasinin KAYNAK SOZLESMESI pinlerini tasir.
    //
    // NEDEN KAYNAK PINI: olculen kusurlarin hepsi TARAYICI davranisidir - iki sekmenin
    // eszamanli 401 yarisi, service worker kaydi, CSP uygulamasi. Bu depoda JS/DOM
    // kosucusu YOKTUR (CLAUDE.md "RIG KOR NOKTASI"), yani CI'da tarayici semantigi
    // pinlenemez. Davranis kaniti dalganin MUHRUNDEKI tarayici olcumleridir; buradaki
    // pinler o davranisi URETEN kaynak kosullarinin sessizce geri alinmasini engeller.
    // MK-6 geregi her pin uretim mutasyonuyla sinanmistir (mutasyon -> TAM 1 isimli
    // kirmizi); mutasyon tablosu muhurde.
    public class GuvenlikFix2bSozlesmeTests
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

        private static int Sayim(string metin, string parca) => metin.Split(parca).Length - 1;

        // ══ CAPA KIRLENMESI - YAPISAL COZUM (UCUNCU KOPYA, BILINCLI) ═══════════════════
        //
        // Sayim/NEG assertleri YORUMSUZ kaynak uzerinde yapilir; aksi halde duzeltmeyi
        // ANLATAN yorum, taranan dizgeyi METIN olarak tasidigi icin sayimi kirletir
        // (bu depoda ALTI kez dusuldu, sonuncusu GF-3).
        //
        // BILINEN MUKERRERLIK: ayni yardimci `GuvenlikFix1SozlesmeTests` ve
        // `GuvenlikFix2aSozlesmeTests` icinde de var - bu UCUNCU kopya. Ortak bir yardimci
        // sinifa cikarmak BASKA dalgalarin pin dosyalarina dokunmayi gerektirdigi icin bu
        // dalgada YAPILMADI; birlestirme raporda ayri kalem olarak isaretlendi.
        private static string KodSatirlari(string kaynak)
        {
            var s = System.Text.RegularExpressions.Regex.Replace(kaynak, "<!--.*?-->", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            return string.Join("\n", s.Split('\n')
                .Select(satir =>
                {
                    // "//" her zaman yorum DEGILDIR: "https://" (onceki karakter ':') ve
                    // regex icindeki kacisli bolu (onceki karakter '\') kesilmez.
                    var i = 0;
                    while (true)
                    {
                        i = satir.IndexOf("//", i, StringComparison.Ordinal);
                        if (i < 0) return satir;
                        var onceki = i > 0 ? satir[i - 1] : '\0';
                        if (onceki != ':' && onceki != '\\') return satir.Substring(0, i);
                        i += 2;
                        if (i >= satir.Length) return satir;
                    }
                }));
        }

        // Bir SINIF METODUNUN govdesini susli parantez sayarak cikarir. Regex YOK: ic ice
        // obje/kapanis literalleri regex'i sessizce yanlis yerden keser.
        private static string MetotGovdesi(string kaynak, string imza)
        {
            var i = kaynak.IndexOf(imza, StringComparison.Ordinal);
            i.Should().BeGreaterThan(-1, $"'{imza}' kaynakta bulunmali");
            var acilis = kaynak.IndexOf('{', i);
            acilis.Should().BeGreaterThan(-1, $"'{imza}' govdesinin acilisi bulunmali");
            var derinlik = 0;
            for (var j = acilis; j < kaynak.Length; j++)
            {
                if (kaynak[j] == '{') derinlik++;
                else if (kaynak[j] == '}')
                {
                    derinlik--;
                    if (derinlik == 0) return kaynak.Substring(acilis, j - acilis + 1);
                }
            }
            throw new InvalidOperationException($"'{imza}' govdesinin kapanisi bulunamadi.");
        }

        // ══ K1 - SEKMELER ARASI REFRESH: KIYAS TABANI *BELLEK* JETONUDUR ═══════════════
        //
        // OLCULEN KIRIK (goz turu): iki sekme eszamanli 401 aldiginda HER SEKMEDE bir
        // refresh atesledi - TOPLAM 2, beklenen TAM 1. Tetikler 741 ms arayla atesledi
        // ama ORTUSTULER.
        //
        // KOK SEBEP: kilit ZATEN VARDI (GF-2a/K10). Kusur kilidin yoklugu degil, kilit
        // icindeki KIYAS TABANIYDI. Kilit ONCESI depodan okunan deger ile kilit ICINDE
        // depodan okunan deger karsilastiriliyordu - STORAGE ile STORAGE. Oysa 401'i
        // DOGURAN jeton, isteğin Authorization basligina konan BELLEK jetonudur. Sekme B
        // tazeleyip depoyu yazinca, sekme A depoyu kilit oncesi de sonrasi da AYNI (taze)
        // degerde gorur, "degismemis" sonucuna varir ve IKINCI ag cagrisini atar.
        //
        // GF-2a'nin K10 pini kilidin VARLIGINI yedi kosulla pinliyordu ama NEYIN
        // KIYASLANDIGINI pinlemiyordu - kusur YESIL BIR PININ ICINDE yasadi. Bu pin tam
        // o boslugu kapatir.
        [Fact]
        public void GF2B_K1_KILIT_ICINDEKI_KIYAS_BELLEK_JETONUYLA_YAPILIR()
        {
            var govde = MetotGovdesi(Oku("frontend/api-client.js"), "async _tryRefresh()");

            // POZ: kiyasin SAG tarafi bellek alani olmali.
            govde.Should().Contain("taze !== this._accessToken",
                "kilitteki kiyas, 401'i DOGURAN bellek jetonuna karsi yapilmali - " +
                "depodan okunan ikinci bir degere karsi DEGIL");

            // ══ ASIL AYIRT EDICI: kilit yolunda depo TEK KEZ okunur ════════════════════
            // Onceki hal `_okuAccessToken()`i IKI kez cagiriyordu (kilit oncesi + kilit
            // icinde) ve kiyas tabani bu yuzden storage'a kaymisti. Sayim 1'i asarsa
            // taban yeniden depoya kaymis demektir - pin o anda kirilir.
            Sayim(KodSatirlari(govde), "_okuAccessToken()").Should().Be(1,
                "kilit yolunda depo TEK KEZ okunmali - ikinci okuma kiyas tabanini " +
                "STORAGE tarafina kaydirir ve mukerrer refresh yeniden dogar");

            // VAKUM KIRICI: govde gercekten kilit yolunu iceriyor olmali; aksi halde
            // yukaridaki iki assert "metot bosalmis" durumunda da yesil kalabilirdi.
            govde.Should().Contain(".request(\"divisima-refresh\"",
                "vakum kirici: olculen govde GERCEKTEN kilitli refresh yolu olmali");
        }

        // ══ K1/b - DIGER SEKMENIN BELLEGI `storage` OLAYIYLA TAZELENIR ═════════════════
        //
        // Kilit icindeki kiyas IKINCI savunma hattidir; birincisi bayat bellek jetonunun
        // HIC olusmamasidir. `setAccessToken` bellegi ve depoyu birlikte yazar ama yalniz
        // YAZAN sekmede; `storage` olayi DIGER sekmelerde atesleyerek onlarin bellek
        // kopyasini esitler.
        [Fact]
        public void GF2B_K1_STORAGE_OLAYI_BELLEGI_ESITLER_ve_YAN_ETKI_URETMEZ()
        {
            var kaynak = Oku("frontend/api-client.js");

            kaynak.Should().Contain("addEventListener(\"storage\"",
                "sekmeler arasi bellek esitlemesi icin `storage` dinleyicisi bulunmali");
            kaynak.Should().Contain("this._accessToken = e.newValue;",
                "dinleyici BELLEK kopyasini olayin yeni degeriyle esitlemeli");

            // ══ YAN ETKI YASAGI - CIFT ANLAM KIRICI ════════════════════════════════════
            // Dinleyici `setAccessToken` CAGIRMAMALI: o metot GF-2a/K8 cikis kancasini
            // tasiyor (SW api kovasi temizligi) ve her sekmede yeniden atesleyerek
            // "ayni kuralin ikinci kopyasi" ailesine girerdi. Ayrica depoya GERI yazar -
            // olayi doguran degeri kaynagina geri yazmak gereksiz bir yazma dongusudur.
            var dinleyici = MetotGovdesi(kaynak, "addEventListener(\"storage\"");
            Sayim(KodSatirlari(dinleyici), "setAccessToken").Should().Be(0,
                "storage dinleyicisi YALNIZ bellegi esitlemeli - cikis kancasini " +
                "ve depoya geri yazmayi tetiklememeli");

            // DAR KAPSAM: yalniz access token anahtari dinlenir.
            dinleyici.Should().Contain("divisima_access_token",
                "dinleyici yalniz access token anahtarina tepki vermeli");
        }
    }
}
