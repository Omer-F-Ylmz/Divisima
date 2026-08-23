using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ DALGA 4 / M10 + M11 - MOBIL DOKUNMA HEDEFI VE KATMAN SOZLESMESI ════════════════════
    //
    // OLCULEN ZARAR (GERCEK CIHAZ, Android/Opera 384x638 - tani katmani ciktisi):
    //     pointerdown -> button#checkoutBtn.btn        trusted=true
    //     touchstart  -> button#checkoutBtn.btn.rippling
    //     pointerup   -> button#checkoutBtn.btn.rippling
    //     touchend    -> button#checkoutBtn.btn.rippling
    //     click       -> span.ripple-ink               trusted=true  idEsit=false
    //        hash: #/ -> #/   *** DEGISMEDI ***
    // Yani mobilde "Sepeti Onayla" HICBIR SEY YAPMIYORDU - satin alma tamamen kapaliydi.
    //
    // MEKANIK HALKA (tarayicida CSSOM ile olculdu, tahmin DEGIL):
    //   1. pointerdown dinleyicisi butonun ICINE <span class="ripple-ink"> ekliyor.
    //   2. .ripple-ink{pointer-events:none} (0,1,0) VARDI - ama ".filter-side.open *,
    //      .cart.on *,.search.on *{pointer-events:auto}" (0,2,0) onu EZIYORDU. Olculdu:
    //      cekmece icindeki ink'in hesaplanan pointer-events degeri "auto".
    //   3. Gercek dokunusta click hedefi ink oluyor; handler'in kati id karsilastirmasi
    //      dusuyor ve closeCart() + hash gecisi ATLANIYOR.
    //   4. EMULASYONDA CURUK GORUNMESININ SEBEBI: sentetik click DOGRUDAN butona gonderilir,
    //      o an ink yoktur. Bu, "gercek cihaz turu neden sart" sorusunun kanitidir.
    //
    // BU PINLERIN SINIRI (DURUST KAYIT): depoda JS/DOM kosucusu YOK (test projesinde
    // AngleSharp/Jint/Playwright yok - olculdu). Tarayici SEMANTIGI (hit-test, ozgulluk,
    // elementFromPoint) bu suitte dogrulanamaz; buradaki pinler KAYNAK SOZLESMESINI tutar:
    // "delege eylem handler'i hedefi closest ile cozer" ve "cerez bari kendi alanindadir".
    // Davranis kaniti tarayicida olculdu, rapora yazildi ve frontend/test/ altinda
    // TEKRARLANABILIR bir olcum betigi olarak depoda duruyor.
    public class FrontendDokunmaHedefiTests
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

        private static string Index => Oku("frontend/index.html");

        // ── M10 (a): SEPET "Sepeti Onayla" ────────────────────────────────────────────────
        [Fact]
        public void SepetOnayHandleri_HEDEFI_closest_ILE_Cozer_ALT_ELEMAN_DUSURMEZ()
        {
            var s = Index;

            s.Should().Contain("cartFoot.addEventListener('click'",
                "on kosul: pinlenen handler hala bu adla bagli olmali - yoksa asagidaki " +
                "assert'ler olcmedikleri bir seyi savunur (VAKUM KIRICI)");

            s.Should().NotContain("e.target.id==='checkoutBtn'",
                "OLCULEN ZARAR: gercek dokunusta click hedefi span.ripple-ink oluyor ve bu " +
                "kati karsilastirma sessizce dusuyordu - mobilde odemeye HIC gecilemiyordu");

            s.Should().Contain("e.target.closest('#checkoutBtn')",
                "hedef, buton YA DA onun herhangi bir alt elemani oldugunda calismali " +
                "(bugun ripple ink, yarin bir ikon/span)");
        }

        // ── M10 (b): FAVORILER "Tumunu sepete ekle" - AYNI SINIF ──────────────────────────
        // Kapsayicisi <aside id="favs" class="cart on"> - yani .cart.on * kurali orada da
        // gecerli. Cihazda ayrica surulmedi; deterministik olarak olculdu (ink hedefiyle
        // gonderilen click handler'i dusurdu, buton hedefiyle calisti).
        [Fact]
        public void FavorileriSepeteEkle_Handleri_HEDEFI_closest_ILE_Cozer()
        {
            var s = Index;

            s.Should().Contain("favFoot.addEventListener('click'",
                "on kosul: handler hala bagli olmali (VAKUM KIRICI)");
            s.Should().NotContain("e.target.id==='favAll'",
                "#favAll de class btn tasiyor - ripple ink ayni sekilde hedefi caliyor");
            s.Should().Contain("e.target.closest('#favAll')");
        }

        // ── M10 (c): SINIF DUZEYI TARAMA ──────────────────────────────────────────────────
        // Bu, tek butonun degil BIR SINIFIN pini. Yeni yazilan her kati hedef karsilastirmasi
        // burada KIRMIZI verir. Bugun ayakta kalmasina izin verilen IKI kullanim, change
        // olayinda checkbox uzerinedir: change KONTROLUN KENDISINDE atesler ve input ELEMAN
        // COCUGU TASIYAMAZ - yani ripple/ikon tuzagi yapisal olarak imkansizdir.
        [Fact]
        public void HICBIR_YENI_EYLEM_HANDLERI_target_id_ILE_KATI_KARSILASTIRMA_YAPMAZ()
        {
            var dosyalar = new[] { "frontend/index.html", "frontend/api-bridge.js", "frontend/admin.html" };
            var izinli = new HashSet<string> { "giftChk", "cmpDiffChk" };
            var bulunan = new List<string>();

            foreach (var f in dosyalar)
                foreach (Match m in Regex.Matches(Oku(f), @"\.target\.id\s*[!=]==?\s*'([^']*)'"))
                    bulunan.Add(m.Groups[1].Value);

            bulunan.Should().OnlyContain(id => izinli.Contains(id),
                "kati e.target.id karsilastirmasi YALNIZ change-olayli checkbox'larda " +
                "guvenlidir; yeni bir tanesi eklendiyse closest ile yazilmali. Bulunanlar: " +
                string.Join(", ", bulunan));

            // CIFT-ANLAM KIRICI: liste bosalirsa (or. iki checkbox da silinirse) yukaridaki
            // assert "hicbir sey yok" diye YESIL kalirdi. Izinli kullanimlarin GERCEKTEN
            // durdugunu dogrula ki tarama vakuma dusmesin.
            bulunan.Should().Contain("giftChk");
            bulunan.Should().Contain("cmpDiffChk");

            // .target.matches( bugun SIFIR (olculdu). Yeni bir tanesi incelemeye girmeli.
            foreach (var f in dosyalar)
                Oku(f).Should().NotContain(".target.matches(",
                    $"{f}: matches de kati hedef karsilastirmasidir - closest kullanilmali");
        }

        // ── M10 (d): CIFT-ANLAM KIRICI - KIMLIK KARSILASTIRMASI KORUNMALI ────────────────
        // "Hepsini closest yap" YANLIS bir duzeltmedir: arka-plana tiklayinca kapanan modal /
        // lightbox kaliplari, hedefin TAM O KATMAN olmasina dayanir. closest'a cevrilseydi
        // modallar ICLERINE her tiklandiginda kapanirdi. Bu pin, duzeltmenin gercekten DAR
        // oldugunu kanitlar.
        [Fact]
        public void ARKA_PLAN_KAPATMA_Handlerlari_KIMLIK_KARSILASTIRMASINI_KORUR()
        {
            var s = Index;
            var koruncak = new (string Desen, string Neden)[]
            {
                ("if(e.target===this)closeSizeChart();", "beden tablosu arka plani"),
                ("if(e.target===this)closeReturn();", "iade modali arka plani"),
                ("if(e.target===this)closeAddrForm();", "adres modali arka plani"),
                ("if(e.target===this)closeCardForm();", "kart modali arka plani"),
                ("if(e.target===lb)window.closeLightbox();", "lightbox arka plani"),
                ("if(e.target===stage)window.closeLightbox();", "lightbox sahnesi"),
                ("if(e.target===modal)close(true);", "genel modal arka plani"),
            };
            foreach (var (desen, neden) in koruncak)
                s.Should().Contain(desen,
                    $"{neden}: kimlik karsilastirmasi KASITLIDIR, closest'a CEVRILMEMELI");

            Oku("frontend/api-bridge.js").Should().Contain("if (e.target === m) m.remove();",
                "api-bridge modali da ayni kaliba sahiptir");
        }

        // ── M10 (e): IKINCIL SAVUNMA - ripple ink hicbir kapsamda isabet hedefi olmamali ──
        [Fact]
        public void RippleInk_CEKMECE_ARAMA_FILTRE_ICINDE_de_ISABET_HEDEFI_OLMAZ()
        {
            var s = Index;

            // Once EZEN kuralin hala orada oldugunu dogrula - yoksa asagidaki assert
            // gerekcesiz bir satiri savunur (VAKUM KIRICI).
            s.Should().Contain(".filter-side.open *,.cart.on *,.search.on *{pointer-events:auto}",
                "on kosul: ripple'in pointer-events:none degerini EZEN kural hala yururlukte");

            s.Should().Contain(
                ".cart.on .ripple-ink,.search.on .ripple-ink,.filter-side.open .ripple-ink{pointer-events:none}",
                "ozgullugu (0,3,0) ile ezen kurali (0,2,0) gecer ve yazarin ozgun niyetini " +
                "TAM O UC KAPSAMDA geri verir");

            s.Should().Contain("animation:rippleAnim .5s ease-out forwards;pointer-events:none}",
                "temel .ripple-ink kurali korunmali - ikincil savunma onun YERINE gecmez");
        }

        // ── M11 + M3: CEREZ BARI KENDI ALANINDA KALIR ────────────────────────────────────
        //
        // OLCULEN ZARAR (tarayici, cerez bari ACIK, cikisli kullanici):
        //   360x640  bar 199-640 (h=441, ekranin %69'u)
        //            "Giris yap" 235-284 -> elementFromPoint: div.ck-text   ULASILAMAZ
        //            alt navigasyonun DORT ogesi de ULASILAMAZ
        //   384x638  bar 217-638 (h=421)  ayni tablo   <- KULLANICININ GERCEK CIHAZI
        //   412x730  bar 326-730 (h=404)  "Giris yap" ulasilir ama alt nav DORT/DORT ORTULU
        // Yani cikisli kullanici odeme sayfasina dusuyor ve oradan CIKAMIYORDU.
        [Fact]
        public void CerezPaneli_hidden_OZNITELIGINE_SAYGI_Duyar()
        {
            var s = Index;

            // KOK SEBEP: display:flex, UA'nin [hidden] kuralini eziyordu.
            s.Should().Contain(".ck-panel{max-width:1180px;margin:14px auto 0;padding:0 22px;display:flex",
                "on kosul: display:flex hala orada - guard tam da bu yuzden gerekli (VAKUM KIRICI)");
            s.Should().Contain(".ck-panel[hidden]{display:none}",
                "panel kapali isaretliyken CIZILMEMELI; aksi halde bar 441 px'e sisip " +
                "altindaki her etkilesimli ogeyi ortuyor ve Ozellestir dugmesi de OLU kaliyor");

            // IDIOM TUTARLILIGI: dosyanin kendi kalibi zaten buydu - korunmali.
            s.Should().Contain(".cmdk[hidden]{display:none}");
            s.Should().Contain(".a11y-panel[hidden]{display:none}");
        }

        [Fact]
        public void CerezBari_MOBILDE_ALT_NAVIGASYONUN_USTUNE_Oturur()
        {
            var s = Index;

            s.Should().Contain(".cookie-bar{bottom:calc(var(--mnav-h,63px) + var(--kb,0px))}",
                "bar, alt navigasyonun OLCULEN yuksekligi kadar yukari alinmali");
            s.Should().Contain(".cookie-bar.gone{transform:translateY(calc(100% + var(--mnav-h,0px)));opacity:0}",
                "kapanis animasyonu da ofseti hesaba katmali - yoksa bar kapanirken " +
                "navigasyonun uzerinde GORUNUR kalir");
            s.Should().Contain("style.setProperty('--mnav-h'",
                "degisken JS tarafinda navigasyonun GERCEK yuksekliginden yazilmali " +
                "(63px yalniz JS'ten onceki yedektir)");

            // CIFT-ANLAM KIRICI: degisken gizliyse 0 yazmali, aksi halde masaustunde bar
            // gerekcesiz sekilde yukari kayardi.
            s.Should().Contain("(gizli?0:mn.offsetHeight)+'px'");
        }
    }
}
