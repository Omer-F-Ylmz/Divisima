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

        // ══ GOZ-FIX EK - IKI KAYNAK SOZLESMESI PINI ═══════════════════════════════════════
        //
        // DURUST ETIKET: asagidaki IKI pin KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR.
        // Depoda JS/DOM kosucusu YOK (sinifin basindaki sinir notu aynen gecerli), bu yuzden
        // "izgarada uydurma sayi gorunmuyor" ya da "sayfa asagi atlamiyor" CI'da dogrulanamaz.
        // Burada tutulan sey, o davranisi ureten KAYNAK KOSULUNUN yerinde durmasidir; davranis
        // kaniti GOZ-FIX raporundaki tarayici once/sonra olcumleridir:
        //   F-G1: 60 beden satirinin 53'u gercekten FARKLI ve 8'i "0" iken gercekte stok VAR
        //         -> duzeltmeden sonra 0/60 ve 0; "Son N urun!" gosteren kart 6 -> 0.
        //   F-Ö2: scrollY 0 -> 648 ve GORUNUR HATA YOK -> duzeltmeden sonra scrollY 0 -> 0 ve
        //         siparis numarasini iceren Turkce hata EKRANDA.
        //
        // Yorumlar taranmadan ONCE ayiklanir: bu depoda bir pinin KENDI belgeledigi kalibi
        // bulup yanlis kirmizi vermesinin bedeli iki kez odendi (Dalga B ve Dalga D kayitlari).

        // Blok yorumlari ve TAM SATIR `//` yorumlarini atar. Satir ici `//` KASITLI OLARAK
        // dokunulmadan birakilir - JS'te "http://" gibi dizgeleri kesmek kaynagi bozardi.
        private static string YorumlariAyikla(string s)
        {
            var bloksuz = Regex.Replace(s, @"/\*[\s\S]*?\*/", " ");
            var satirlar = new List<string>();
            foreach (var satir in bloksuz.Split('\n'))
                if (!satir.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    satirlar.Add(satir);
            return string.Join("\n", satirlar);
        }

        // Imzadan baslayip susli parantez esleyerek fonksiyon govdesini cikarir.
        private static string FonksiyonGovdesi(string kaynak, string imza)
        {
            var bas = kaynak.IndexOf(imza, StringComparison.Ordinal);
            bas.Should().BeGreaterThan(-1, $"pinlenen fonksiyon kaynakta bulunmali: {imza}");
            var i = kaynak.IndexOf('{', bas);
            i.Should().BeGreaterThan(-1, "fonksiyon govdesi acilmali");
            int derinlik = 0, j = i;
            for (; j < kaynak.Length; j++)
            {
                if (kaynak[j] == '{') derinlik++;
                else if (kaynak[j] == '}' && --derinlik == 0) break;
            }
            derinlik.Should().Be(0, "susli parantezler eslesmeli");
            return kaynak.Substring(i, j - i + 1);
        }

        // ── P1 (F-G1): IZGARA STOGU UYDURULMAZ, KITLIK IDDIASI GERCEK SAYIDAN TURER ───────
        [Fact]
        public void KAYNAK_SOZLESMESI_IzgaraStogu_PRNG_ile_URETILMEZ_ve_KitlikMetni_GERCEK_STOKTAN_Turer()
        {
            var ham = Index;
            var s = YorumlariAyikla(ham);

            // VAKUM KIRICI 1: tarama gercekten bir govde okumus olmali.
            var govde = FonksiyonGovdesi(s, "function sizeStockOf(p)");
            govde.Length.Should().BeGreaterThan(80, "sizeStockOf govdesi bos okunmus olamaz");

            // VAKUM KIRICI 2: `rngOf` dosyada HALA kullaniliyor olmali (yorumlar, renk/degerlendirme
            // uretimi). Yardimci tumden silinseydi asagidaki iddia BEDAVA dogru olurdu.
            Regex.Matches(s, @"rngOf\s*\(").Count.Should().BeGreaterThan(1,
                "rngOf baska yerlerde kullanilmaya devam ediyor - tarama vakuma dusmemeli");

            // ASIL SOZLESME: beden stogu tohumlu rastgelelikten TUREMEZ.
            govde.Should().NotContain("rngOf",
                "beden bazli stok UYDURULMAZ; bilinmiyorsa BOS harita donulur ve urun toplami " +
                "sunucunun verdigi total_stock'tan (p.stock) okunur");
            govde.Should().NotContain("Math.random",
                "ayni gerekce: rastgelelik envanter sayisi uretemez");

            // Bilinmeyen kirilim BOS harita ile temsil edilir (cagiranlar 'anahtar yok' = kisit yok).
            govde.Should().Contain("return {};",
                "bilinmeyen beden kirilimi BOS harita ile temsil edilmeli");

            // stockOf, BOS haritayi 0 sayip urunu yanlislikla 'Tukendi' gostermemeli.
            var stokGovde = FonksiyonGovdesi(s, "function stockOf(p)");
            stokGovde.Should().Contain("Number(p.stock)",
                "kirilim bilinmiyorsa sunucunun verdigi toplam kullanilmali");

            // KITLIK IDDIASI: yalniz stok BILINIYORSA yazilir.
            s.Should().Contain("_stokBilinir=isFinite(Number(p.stock))",
                "'Son N urun!' bir TICARI IDDIADIR - sunucudan gercek sayi gelmediyse gosterilmez");
            s.Should().Contain("lowS=(!sold&&_stokBilinir&&_gercekToplam<=5)?_gercekToplam:0",
                "kitlik sayisi GERCEK toplamdan turemeli");

            // CIFT-ANLAM KIRICI: eski, kosulsuz bicim GERI GELEMEZ. Bu olmadan "stokBilinir
            // degiskenini tanimla ama kullanma" gibi bir uygulama da pinden gecerdi.
            s.Should().NotContain("lowS=(!sold&&stockOf(p)<=5)?stockOf(p):0",
                "stok bilinmese de kitlik yazan eski bicim geri gelmemeli");
        }

        // ── P2 (F-Ö2): GORUNUR ICERIK YOKSA KAYDIRMA YOK, GORUNUR HATA VAR ───────────────
        [Fact]
        public void KAYNAK_SOZLESMESI_OdemeGomme_GORUNUR_ICERIK_YOKSA_Kaydirmaz_ve_GORUNUR_HATA_Yazar()
        {
            var s = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // VAKUM KIRICI: kaydirma OZELLIGI hala var - sadece silinmis olsaydi asagidaki
            // "kosullu" iddiasi anlamsiz sekilde dogru cikardi.
            s.Should().Contain("scrollIntoView",
                "gercek form geldiginde kaydirma davranisi KORUNMALI");

            var govde = FonksiyonGovdesi(s, "function embedCheckoutForm(html)");
            govde.Length.Should().BeGreaterThan(200, "embedCheckoutForm govdesi bos okunmus olamaz");

            // ASIL SOZLESME: kaydirma KOSULLU ve kosul kaydirmadan ONCE geliyor.
            govde.Should().Contain("getBoundingClientRect().height > 0",
                "bos (0 px) bir host'a kaydirma yapilmamali - olculdu: scrollY 0 -> 648 ve " +
                "ekranda hicbir sey yoktu");
            var kosulYeri = govde.IndexOf("getBoundingClientRect().height > 0", StringComparison.Ordinal);
            var kaydirmaYeri = govde.IndexOf("scrollIntoView", StringComparison.Ordinal);
            kosulYeri.Should().BeLessThan(kaydirmaYeri,
                "kosul kaydirmadan ONCE gelmeli - sonra gelen bir kontrol atlamayi engellemez");

            // GORUNUR ICERIK OLCUTU tanimli ve odeme yolunda CAGRILIYOR olmali.
            Regex.Matches(s, @"odemeFormuGorunurMu\s*\(").Count.Should().BeGreaterThan(1,
                "olcut yalniz TANIMLI degil, submitOrder icinde CAGRILMIS da olmali " +
                "(tanim + en az bir cagri)");

            var gonder = FonksiyonGovdesi(s, "async function submitOrder()");
            gonder.Should().Contain("if (!odemeFormuGorunurMu(pay.checkout_form_content))",
                "gorunur icerik yoksa gomme yoluna HIC girilmemeli");

            // SESSIZ BASARISIZLIK YASAK: kullaniciya GORUNUR metin yazilmali.
            gonder.Should().Contain("checkoutHatasiYaz(",
                "hata ekrandaki #coErr alanina yazilmali - konsol son kullanicida SESSIZDIR");
            gonder.Should().Contain("ÖDENMEMİŞ",
                "kullaniciya siparisin ODENMEDIGI acikca soylenmeli");

            // CIFT-ANLAM KIRICI: 401 dali AYRI ve eylem iceren bir metin vermeli; aksi halde
            // "her hataya ayni genel metni yaz" uygulamasi da bu pinden gecerdi.
            gonder.Should().Contain("e.status === 401",
                "oturum bitmesi ayri ele alinmali");
            gonder.Should().Contain("Oturumun sona erdi, lütfen tekrar giriş yap.",
                "401'de kullaniciya NE YAPACAGI soylenmeli");
        }

        // ── P3 (F-D1): YORUM/YILDIZ UYDURULMAZ, YALNIZ GERCEK ALANDAN TURER ─────────────
        // DURUST ETIKET: bu bir KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR. Depoda
        // JS/DOM kosucusu yok; davranis kaniti VITRIN-FIX-2 raporundaki tarayici olcumudur
        // (once: 24 urun icin 1630 uydurma yorum iddiasi ve JSON-LD aggregateRating 4.5/8;
        // sonra: 0 ve aggregateRating YOK - veritabaninda product_reviews 0 satir).
        [Fact]
        public void KAYNAK_SOZLESMESI_Yorumlar_PRNG_ile_URETILMEZ_ve_Yildiz_GERCEK_ALANDAN_Turer()
        {
            var s = YorumlariAyikla(Index);

            // VAKUM KIRICI 1: `rngOf` dosyada HALA kullaniliyor (fit/renk/kumas yuzeyleri bu
            // dalganin kapsami DISINDA). Yardimci tumden silinseydi asagidaki iddia BEDAVA
            // dogru olurdu.
            Regex.Matches(s, @"rngOf\s*\(").Count.Should().BeGreaterThan(1,
                "rngOf baska yuzeylerde kullanilmaya devam ediyor - tarama vakuma dusmemeli");

            // VAKUM KIRICI 2: tarama gercekten bir govde okumus olmali.
            var govde = FonksiyonGovdesi(s, "function reviewsOf(p)");
            govde.Length.Should().BeGreaterThan(120, "reviewsOf govdesi bos okunmus olamaz");

            // ASIL SOZLESME: yorum verisi tohumlu rastgelelikten TUREMEZ.
            govde.Should().NotContain("rngOf",
                "yildiz ve yorum sayisi bir TICARI BEYANDIR - uydurulamaz");
            govde.Should().NotContain("Math.random", "ayni gerekce");

            // Kaynak SUNUCU: average_rating / review_count (api-bridge.js mapProduct esler).
            govde.Should().Contain("Number(p.rating)", "ortalama sunucudan gelmeli");
            govde.Should().Contain("p.rvcount", "yorum sayisi sunucudan gelmeli");

            // UYDURMA HAVUZLARI DEPODAN KALKMIS OLMALI (tanim duzeyinde).
            s.Should().NotContain("var RV_NAMES=", "uydurma musteri isimleri kaldirildi");
            s.Should().NotContain("var RV_TR=", "uydurma yorum metinleri kaldirildi");
            s.Should().NotContain("var RV_EN=", "uydurma yorum metinleri kaldirildi");
            s.Should().NotContain("var RV_AGO_TR=", "uydurma tarih damgalari kaldirildi");

            // KART YILDIZI: yalniz GERCEK yorum varsa cizilir.
            s.Should().Contain("(rv.count>0?'<div class=\"card-rate\">",
                "yorumu olmayan urunun kartinda yildiz blogu HIC cizilmemeli");

            // CIFT-ANLAM KIRICI: eski KOSULSUZ bicim geri gelemez. Bu olmadan "sayiyi
            // sunucudan al ama blogu yine her kartta ciz" uygulamasi da pinden gecerdi.
            s.Should().NotContain("'</div><div class=\"card-rate\">'+starsHTML",
                "her kartta kosulsuz yildiz basan eski bicim geri gelmemeli");

            // "DOGRULANMIS ALICI" ROZETI: ProductReviewResponseDto is_verified_purchase
            // TASIMIYOR (entity'de var, DTO'da yok - olculdu), dolayisiyla rozet
            // CIZILEMEZ. reviewCards govdesinde gecmemeli.
            var kartGovde = FonksiyonGovdesi(s, "function reviewCards(p,limit)");
            kartGovde.Should().NotContain("rv_verified",
                "rozet gercek bir alandan gelmiyorsa HIC gosterilmez");
            kartGovde.Should().NotContain("rv-verify", "ayni gerekce");
            kartGovde.Should().Contain("o.comment",
                "yorum metni sunucudan gelen alandan okunmali");

            // DURUST BOS DURUM: yorum yoksa gorunur bir metin yazilir.
            s.Should().Contain("rv_empty:['Bu ürün için henüz yorum yok.'",
                "bos durum metni tanimli olmali");
            var bolumGovde = FonksiyonGovdesi(s, "function reviewsSection(p)");
            bolumGovde.Should().Contain("t('rv_empty')",
                "yorum yoksa GORUNUR ve DURUST bir bos durum cizilmeli");

            // KOPRU: gercek alanlar eslenmis ve yorum ucu GERCEKTEN cagriliyor olmali
            // (tanim + en az bir cagri).
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            b.Should().Contain("rating: Number(p.average_rating) || 0",
                "liste yolu gercek ortalamayi tasimali");
            b.Should().Contain("rvcount: Math.max(0, Math.floor(Number(p.review_count) || 0))",
                "liste yolu gercek yorum sayisini tasimali");
            b.Should().Contain("api.reviews.forProduct(",
                "yorum metinleri gercek uctan cekilmeli");
            Regex.Matches(b, @"yorumlariCiz\s*\(").Count.Should().BeGreaterThan(1,
                "yukleyici yalniz TANIMLI degil, detay acilisinda CAGRILMIS da olmali");
        }

        // ── P4 (F-A1): GIRISTEN SONRAKI ILK SENKRON SILMEZ, BIRLESTIRIR ────────────────
        // DURUST ETIKET: kaynak sozlesmesi pini. Davranis kaniti raporda - kontrollu A/B:
        // eski kodla sunucu sepeti 2 aktif -> 0 aktif (KALICI SEPET SILINDI), yeni kodla
        // yerel 0 -> 2 ve sunucu 2 aktif -> 2 aktif.
        [Fact]
        public void KAYNAK_SOZLESMESI_IlkSenkron_SILMEZ_Birlestirir_Ayna_SONRA_Baslar()
        {
            var s = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            var govde = FonksiyonGovdesi(s, "async function syncCartToServer()");
            govde.Length.Should().BeGreaterThan(300, "syncCartToServer govdesi bos okunmus olamaz");

            // VAKUM KIRICI: AYNA SILME HALA VAR. Silme tumden kaldirilsaydi "ilk senkron
            // silmez" iddiasi BEDAVA dogru olurdu - ve sepet kaymasi geri gelirdi.
            govde.Should().Contain("api.cart.remove(",
                "birlestirmeden SONRAKI senkronlar yerelde olmayani silmeye devam etmeli");

            // ASIL SOZLESME: ilk gecis ayri bir dal ve o dal silmeye HIC ULASMIYOR.
            govde.Should().Contain("if (!ilkSenkronYapildi)", "ilk gecis ayri ele alinmali");
            var ilkDalBas = govde.IndexOf("if (!ilkSenkronYapildi)", StringComparison.Ordinal);
            var aynaBas = govde.IndexOf("var local = cartItemsPayload();", StringComparison.Ordinal);
            ilkDalBas.Should().BeGreaterThan(-1);
            aynaBas.Should().BeGreaterThan(ilkDalBas, "ayna duzeni ilk gecisten SONRA gelmeli");
            var ilkDal = govde.Substring(ilkDalBas, aynaBas - ilkDalBas);
            ilkDal.Should().NotContain("api.cart.remove(",
                "GIRISTEN SONRAKI ILK SENKRON HICBIR SEYI SILMEZ - olculen zarar tam buydu");
            ilkDal.Should().Contain("return;",
                "ilk gecis kendi dalinda BITMELI, ayna dongusune AKMAMALI");

            // BIRLESTIRME: sunucu kalemleri yerele iner ve CAKISMADA YEREL KAZANIR.
            s.Should().Contain("function sunucuKalemleriniBirlestir(", "birlestirici tanimli olmali");
            var birlestir = FonksiyonGovdesi(s, "function sunucuKalemleriniBirlestir(server)");
            birlestir.Should().Contain("if (yerel[k]) continue;",
                "ayni urun+beden iki tarafta da varsa YEREL adet kazanmali");
            birlestir.Should().Contain("window.cart.set(",
                "sunucuda olup yerelde olmayan kalem YERELE INMELI");

            // KATALOGDA OLMAYAN URUN: yerele indirilemez (renderCart onu siler), bu yuzden
            // SILINMEKTEN de korunur. Bu olmadan "asla silmez" ikinci gecisde yalan olurdu.
            birlestir.Should().Contain("korunanSunucuAnahtarlari[k] = true",
                "katalogda bulunamayan sunucu kalemi korumaya alinmali");
            govde.Should().Contain("korunanSunucuAnahtarlari[k]",
                "ayna dongusu korunan anahtarlari ATLAMALI");

            // CIFT-ANLAM KIRICI: bayrak HEM giriste HEM cikista yeniden silahlanmali.
            // Yalniz tanim olsaydi (0 cagri) birlestirme yalnizca ilk sayfa yuklemesinde
            // calisir, ikinci bir kullanicinin girisinde ESKI zarar geri gelirdi.
            Regex.Matches(s, @"sepetBirlestirmesiniSilahlandir\s*\(\s*\)").Count.Should().BeGreaterThan(2,
                "tanim + giris + cikis: en az uc gecis olmali");

            // TEK ISTEK: eski kod `.items` bos dustugunde ayni ucu IKINCI KEZ cagiriyordu.
            Regex.Matches(govde, @"api\.cart\.get\s*\(").Count.Should().Be(1,
                "sunucu sepeti tur basina TEK kez okunmali");
        }


        // ── P5 (MFIX-1 / F-M3a + F-M3b): MOCK CHECKOUT DIRILEMEZ, TEK GERCEK CHECKOUT ──
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR (depoda JS/DOM
        // kosucusu yok). Davranis kaniti MFIX-1 raporundaki REPRO-1 ve REPRO-3 tarayici
        // olcumleridir: kupon uygulandiginda ve dil degistirildiginde mock ARTIK GELMIYOR
        // (coSteps=false, coSubmit=true) ve girisli kullanici "Continuing as guest" GORMUYOR.
        [Fact]
        public void KAYNAK_SOZLESMESI_MockCheckout_Dirilemez_ve_TekGercekCheckout()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var s = YorumlariAyikla(Index);

            // VAKUM KIRICI 1: GERCEK cizici HALA VAR. Mock'u etkisizlestirmenin dogru yolu
            // gercegi de silmek DEGILDIR; silinseydi asagidaki iddia BEDAVA dogru olurdu.
            b.Should().Contain("async function renderRealCheckout()",
                "gercek checkout cizicisi yerinde durmali");
            b.Should().Contain("function misafirCheckoutCiz(",
                "gercek MISAFIR cizicisi yerinde durmali");

            // VAKUM KIRICI 2: index.html'in DIRILIS YOLLARI HALA ORADA. Duzeltme
            // "cagiranlari sil" DEGIL "hedefi etkisizlestir"; cagiranlar silinseydi pin
            // yanlis bir mekanizmayi savunurdu.
            Regex.Matches(s, @"renderCheckout\s*\(\s*\)").Count.Should().BeGreaterThan(3,
                "kupon/para birimi/dil yollari hala renderCheckout cagiriyor olmali");

            // ASIL SOZLESME: api-bridge index.html'in cizicisini SARMALAYIP EZIYOR.
            b.Should().Contain("window.renderCheckout = gercekCizim",
                "mock cizici gercek cizimle EZILMELI");
            b.Should().Contain("window.showCheckout = gercekGoster",
                "showCheckout de EZILMELI - router yolu da kapanmali");
            b.Should().Contain("gercekCizim.__divisimaGercek = true",
                "cift sarmalamayi engelleyen bayrak bulunmali");

            // Ezen fonksiyon GERCEKTEN gercek cizimi cagirmali (bos bir stub olmamali).
            var cizimGovde = FonksiyonGovdesi(b, "var gercekCizim = function ()");
            cizimGovde.Should().Contain("renderRealCheckout()",
                "ezen cizici GERCEK checkout'u cagirmali");

            // CIFT-ANLAM KIRICI 1: cizim YALNIZ odeme rotasinda yapilmali. Kosul olmasaydi
            // kupon/dil degisimi BASKA sayfalarda da checkout cizerdi.
            cizimGovde.Should().Contain("\"odeme\"",
                "cizim yalnizca #/odeme rotasinda yapilmali");

            // CIFT-ANLAM KIRICI 2: showCheckout CIZMEMELI - cizimi router'in ardindan kosan
            // handle() yapar. Ikisi de cizseydi her gezinmede IKI kez cizilirdi.
            var gosterGovde = FonksiyonGovdesi(b, "var gercekGoster = function ()");
            gosterGovde.Should().NotContain("renderRealCheckout",
                "showCheckout yalniz gorunumu acar, CIZMEZ (cift cizim olmasin)");
            gosterGovde.Should().Contain("setView",
                "showCheckout gorunumu acmali");

            // Cekmecede sunucu-dogrulamali kupon CHECKOUT'A TASINMALI.
            b.Should().Contain("window.divisimaSetCheckoutCoupon = function",
                "cekmece kuponu checkout'a tasiyan koprü tanimli olmali");

            // ── IKINCI SAVUNMA HATTI (L3 cift-kor denetcisi buldu) ──────────────────
            // api-bridge `defer` ile yuklenir; index.html'in inline script'i acilista
            // KOSULSUZ router() cagirir. Yani sayfa DOGRUDAN #/odeme ile acilirsa ezme
            // HENUZ OLMAMIS olur ve orijinal govde mock'u cizerdi (canli kart formu +
            // coFinish dahil). api-bridge hic yuklenmezse mock KALICI canli kalirdi.
            // Bu yuzden mock KAYNAKTA da etkisizlestirildi: govde ERKEN DONER.
            s.Should().Contain("<script src=\"/api-bridge.js\" defer>",
                "yukleme sirasi varsayimi degisirse bu pin yeniden dusunulmeli");
            var mockGovde = FonksiyonGovdesi(s, "function renderCheckout()");
            // NOT (5. KONTROL YAKALADI): duz IndexOf("return;") YETMEZ - mock govdesinde
            // BASKA bir return daha var (bos sepet dali) ve erken donus kaldirilsa bile o
            // eslesirdi, yani pin ZAAFLIYDI. Ayrica regex'te ters bolu kacisi bu depoda
            // yazim zincirinde KAYBOLABILIYOR (CLAUDE.md dersi). Bu yuzden kacissiz ve
            // KOMSULUK tabanli olculur: notr yer tutucudan SONRAKI satir kosulsuz return
            // olmali. Mutasyon return'u yorumlarsa YorumlariAyikla o satiri SILER ve
            // komsuluk BOZULUR -> pin kirmizi olur.
            var mockSatirlari = mockGovde.Split('\n');
            var yerTutucuSatiri = -1;
            for (var i = 0; i < mockSatirlari.Length; i++)
            {
                if (mockSatirlari[i].Contains("Ödeme hazırlanıyor"))
                {
                    yerTutucuSatiri = i;
                    break;
                }
            }
            yerTutucuSatiri.Should().BeGreaterThan(-1,
                "mock govdesi notr yer tutucuyu yazmali - api-bridge yuklenmeden ONCE de mock CIZMEMELI");
            (yerTutucuSatiri + 1).Should().BeLessThan(mockSatirlari.Length,
                "yer tutucudan sonra en az bir satir olmali");
            mockSatirlari[yerTutucuSatiri + 1].Trim().Should().Be("return;",
                "yer tutucudan HEMEN SONRAKI satir KOSULSUZ return olmali (erken donus)");
            var erkenDonus = yerTutucuSatiri;

            // VAKUM KIRICI: mock uretici HALA govdede (silinmedi, ERISILEMEZ kilindi).
            // Silinseydi "erken donus mock'tan once" iddiasi BEDAVA dogru olurdu.
            var mockUreticiSatiri = -1;
            for (var i = 0; i < mockSatirlari.Length; i++)
            {
                if (mockSatirlari[i].Contains("coStepBar()"))
                {
                    mockUreticiSatiri = i;
                    break;
                }
            }
            mockUreticiSatiri.Should().BeGreaterThan(-1,
                "mock uretici govdede duruyor olmali (sokum degil, erisilemezlik)");
            erkenDonus.Should().BeLessThan(mockUreticiSatiri,
                "erken donus mock URETIMINDEN ONCE gelmeli");
        }

        // ── P6 (MFIX-1 / F-M3f + F-M3a): REQUEST_ID OTURUM BASINA, SAHTE KUPON TABLOSU YOK ──
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti REPRO-2: tek oturumda
        // uc tik -> TEK siparis (218); yeniden yuklenen oturumda 1. tik YENI siparis (219),
        // 2. tik "zaten olusturulmustu" ve YENI siparis YOK. DB ile dogrulandi.
        [Fact]
        public void KAYNAK_SOZLESMESI_RequestId_OturumBasina_ve_SahteKuponTablosu_Yok()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var s = YorumlariAyikla(Index);

            // ── REQUEST_ID ───────────────────────────────────────────────────────────
            // ASIL SOZLESME: IKI siparis yolu da (uye + misafir) AYNI oturum anahtarini alir.
            Regex.Matches(b, @"request_id:\s*checkoutIstekIdAl\(\)").Count.Should().Be(2,
                "uye ve misafir yollarinin IKISI de oturum anahtarini kullanmali");

            // CIFT-ANLAM KIRICI: tik basina uretim GERI GELEMEZ. Eski bicim request_id
            // satirinda dogrudan crypto.randomUUID()/Date.now() cagiriyordu.
            Regex.Matches(b, @"request_id:\s*\(window\.crypto").Count.Should().Be(0,
                "tik basina anahtar ureten eski bicim geri gelmemeli");
            Regex.Matches(b, @"request_id:\s*""mg-""").Count.Should().Be(0,
                "misafir yolundaki tik basina uretim de geri gelmemeli");

            // Yardimcilar TANIMLI ve CAGRILMIS olmali (yalniz tanim = olu kod).
            b.Should().Contain("function checkoutIstekIdAl()", "anahtar uretici tanimli olmali");
            b.Should().Contain("function checkoutIstekIdYenile()", "yenileyici tanimli olmali");
            b.Should().Contain("function checkoutIstekIdSepeteGoreTazele()", "sepet tazeleyici tanimli olmali");
            Regex.Matches(b, @"checkoutIstekIdSepeteGoreTazele\s*\(\s*\)").Count.Should().BeGreaterThan(1,
                "tanim + checkout gonderiminde cagri: en az iki gecis");
            Regex.Matches(b, @"checkoutIstekIdYenile\s*\(\s*\)").Count.Should().BeGreaterThan(2,
                "tanim + sepet degisiminde + BASARILI sipariste: en az uc gecis");

            // BASARILI sipariste yenileme SART - yoksa anahtar sonsuza kadar donar ve
            // musteri IKINCI bir siparis VEREMEZ.
            b.Should().Contain("if (ok) checkoutIstekIdYenile();",
                "siparis tamamlandiginda anahtar yenilenmeli");

            // Sunucunun "zaten olusturulmus" yaniti kullaniciya ACIKCA soylenmeli.
            b.Should().Contain("zaten olu", "replay yaniti tespit edilmeli");
            b.Should().Contain("YENİ bir sipariş oluşturulmadı",
                "kullaniciya yeni siparis olusmadigi soylenmeli");

            // ── SAHTE KUPON TABLOSU [YOKLUK] ─────────────────────────────────────────
            var ham = Oku("frontend/index.html") + "\n" + Oku("frontend/api-bridge.js");
            foreach (var kod in new[] { "HOSGELDIN", "STIL20", "KARGOBEDAVA", "NAKIT250" })
                Regex.Matches(ham, Regex.Escape(kod)).Count.Should().Be(0,
                    $"uydurma kupon kodu '{kod}' frontend'de HIC gecmemeli (tablo, i18n reklami ve bulten vaadi dahil)");

            // YOKLUK IDDIASININ NEGATIF KONTROLU: tarama gercekten calisiyor olmali.
            Regex.Matches(ham, "cp_apply").Count.Should().BeGreaterThan(0,
                "tarama vakuma dusmemeli - bilinen bir dizge BULUNMALI");

            // Yerel sahte tablo ve onu okuyan fonksiyon SOKULDU.
            Regex.Matches(s, @"var COUPONS\s*=").Count.Should().Be(0,
                "yerel sahte kupon tablosu kaldirilmis olmali");
            Regex.Matches(s, @"function applyCoupon\s*\(").Count.Should().Be(0,
                "yerel tabloyu sorgulayan applyCoupon kaldirilmis olmali");

            // VAKUM KIRICI: kupon OZELLIGI silinmedi - kutu ve kaldirma HALA VAR.
            s.Should().Contain("function couponUI()", "cekmece kupon kutusu yerinde durmali");
            s.Should().Contain("function removeCoupon()", "kupon kaldirma yerinde durmali");

            // ASIL SOZLESME: dogrulama SUNUCUDAN.
            var uygulaGovde = FonksiyonGovdesi(s, "async function couponApplyFrom(scope)");
            uygulaGovde.Should().Contain("window.divisimaValidateCoupon",
                "kupon dogrulamasi SUNUCU ucuna gitmeli");
            uygulaGovde.Should().Contain("d.discount_amount",
                "indirim tutari SUNUCUNUN dondurdugu deger olmali");

            // CIFT-ANLAM KIRICI: sunucu reddederse yerel bir indirim UYGULANMAMALI.
            // NOT: kaynak SIKISTIRILMIS (bosluksuz). Bosluga duyarli bir assert kendi
            // bicimlendirme varsayimini olcerdi - regex bosluga TOLERANSLI.
            Regex.IsMatch(uygulaGovde, @"if\s*\(\s*!d\s*\)").Should().BeTrue(
                "sunucu reddinde erken donulmeli");
            uygulaGovde.Should().Contain("cp_invalid",
                "reddedilen kod icin GORUNUR hata mesaji olmali");
        }
    }
}
