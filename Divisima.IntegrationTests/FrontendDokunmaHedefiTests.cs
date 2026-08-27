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
            // MFIX-2: "giftChk" IZINLI LISTEDEN CIKTI - o checkbox MOCK CHECKOUT'un
            // hediye paketi adimindaydi ve MFIX-1 devri kapsaminda SOKULDU. Kural
            // DEGISMEDI (kati e.target.id yalniz change-olayli checkbox'ta guvenli);
            // liste, mesru bir uyesi kaldirildigi icin daraldi.
            var izinli = new HashSet<string> { "cmpDiffChk" };
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
            // MFIX-3b PREMIS: `rngOf` SOKULDU - variantsOf onun SON CAGIRANIYDI. Vakum kirici
            // artik "tarama dosyayi GERCEKTEN okudu" olcutune dayaniyor (ayni guvence).
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali (vakum kirici)");

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
            // MFIX-3b PREMIS (merkez onayina): metin i18n sozlugune tasindi; olcut ARTIK
            // ANAHTAR. Iddia ZAYIFLAMIYOR - anahtarin T ve AR sozlugunde BULUNDUGUNU P11
            // ayrica pinliyor (MFIX-3 kalibi).
            gonder.Should().Contain("b_odenmemis_duruyor",
                "kullaniciya siparisin ODENMEDIGI acikca soylenmeli");

            // CIFT-ANLAM KIRICI: 401 dali AYRI ve eylem iceren bir metin vermeli; aksi halde
            // "her hataya ayni genel metni yaz" uygulamasi da bu pinden gecerdi.
            gonder.Should().Contain("e.status === 401",
                "oturum bitmesi ayri ele alinmali");
            // PREMIS DEGISIKLIGI (MFIX-3 / F-M2, merkez onayina sunuldu): metin ARTIK
            // api-bridge'te GOMULU DEGIL, sozlukte. Assert'in OLCTUGU SEY DEGISMEDI -
            // "401 dalinda kullaniciya eylem iceren AYRI bir metin verilir" - yalnizca
            // metnin YERI degisti. Anahtarin sozlukte GERCEKTEN bulundugunu P11 ayrica
            // pinliyor, yani iddia ZAYIFLAMADI, iki pine BOLUNDU.
            gonder.Should().Contain("ceviri(\"err_session\")",
                "401'de kullaniciya NE YAPACAGI soylenmeli (metin sozlukten gelir)");
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
            // MFIX-3b PREMIS: `rngOf` SOKULDU (son cagirani variantsOf idi).
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali (vakum kirici)");

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

            // MFIX-2'DE BILINCLI DEGISTIRILDI. MFIX-1'de buradaki vakum kirici "mock uretici
            // HALA govdede (silinmedi, ERISILEMEZ kilindi)" diyordu; o gun DOGRUYDU cunku
            // uretici ADDR/CARDS ile ic ice oldugu icin silinememisti. MFIX-2'de merkez
            // SOKUMU ACIKCA emretti (MFIX-1 devri) ve 0c haritasi bagi cozdu, dolayisiyla
            // uretici ARTIK YOK - eski assert bugun SOKULMEMIS olmasini SAVUNURDU.
            // Yerine GECEN iddia daha gucludur: govde YALNIZ yer tutucu + return tasir,
            // yani "erken donus mock'tan once" degil "MOCK URETIMI HIC YOK".
            mockGovde.Should().NotContain("coStepBar",
                "mock uretici SOKULDU - govdede uretim izi kalmamali");
            mockGovde.Should().NotContain("coSummaryHTML",
                "mock ozet uretici de SOKULDU");
            // VAKUM KIRICI (yenisi): govde BOS okunmus olamaz - yer tutucu satiri BULUNDU
            // ve o satirdan sonra en az bir satir var (yukarida assert edildi); ayrica
            // fonksiyon govdesi anlamli bir uzunlukta olmali.
            mockGovde.Trim().Length.Should().BeGreaterThan(40,
                "govde gercekten okunmus olmali - bos string uzerinde NotContain BEDAVA gecerdi");
            erkenDonus.Should().BeGreaterThan(-1, "erken donus satiri bulunmus olmali");
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
            // MFIX-3b PREMIS (merkez onayina): metin i18n sozlugune tasindi; olcut ARTIK ANAHTAR.
            b.Should().Contain("b_yeni_siparis_yok",
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

        // ── P7 (MFIX-2 / F-M9 + F-M6): IKNA YUZEYLERI PRNG ILE URETILMEZ,
        //    GERCEK VERI YOKSA SATIR CIZILMEZ ────────────────────────────────────
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR (depoda
        // JS/DOM kosucusu yok - Dalga 4'ten beri acik kalem). Davranis kaniti MFIX-2
        // raporundaki R-M9/R-M6 KONTROLLU A/B olcumleridir: ayni tarayicida yedek surum
        // servis edilip olculdu, sonra yeni surum. ONCE deri kemerde "12 kisi su an bu
        // urune bakiyor", fit cubugu, "3 x 190 TL taksit", model satiri, kalip onerisi,
        // uydurma kumas ve "Istanbul icin ... Hizli Teslimat" vardi; SONRA yedisi de YOK.
        [Fact]
        public void KAYNAK_SOZLESMESI_IknaYuzeyleri_PRNG_Uretilmez_ve_GercekVeriYoksaSatirYok()
        {
            var s = YorumlariAyikla(Index);
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // ── VAKUM KIRICI 1: rngOf'un KENDISI HALA VAR ────────────────────────
            // Duzeltme "PRNG'yi sil" DEGIL "IKNA YUZEYLERINI gercek veriye bagla".
            // rngOf renk/gorsel gibi KAPSAM DISI yuzeylerde kullanilmaya devam ediyor;
            // silinseydi asagidaki iddialar BEDAVA dogru olurdu.
            // MFIX-3b PREMIS: `rngOf` SOKULDU (son cagirani variantsOf idi).
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali (vakum kirici)");

            // ── ASIL SOZLESME: uydurma ureticiler ve tuketicileri YOK ────────────
            foreach (var ad in new[] { "fitInfo", "fitPanel", "detailsOf",
                                       "viewingHTML", "pdViewing", "_viewInt",
                                       "instHTML", "pdInst",
                                       "SIZE_TABLE", "SIZE_CONV",
                                       "CARE_STD", "CARE_DRY" })
            {
                Regex.Matches(s, @"\b" + Regex.Escape(ad) + @"\b").Count.Should().Be(0,
                    $"uydurma ikna yuzeyi '{ad}' kaynakta HIC gecmemeli (yorumlar ayiklanmis halde)");
            }
            Regex.Matches(s, @"\bvar\s+FABRIC\b").Count.Should().Be(0, "uydurma kumas havuzu kalmamali");
            Regex.Matches(s, @"\bvar\s+FITS\b").Count.Should().Be(0, "uydurma kalip havuzu kalmamali");

            // ── GERCEK VERIYE BAGLANDI: uc GERCEK uc da kaynakta olmali ─────────
            b.Should().Contain("/api/product-attribute/product/",
                "urun ozellikleri GERCEK attribute ucundan gelmeli");
            b.Should().Contain("/api/size-guide/category/",
                "beden tablosu GERCEK size-guide ucundan gelmeli");
            b.Should().Contain("api.address.list()",
                "teslimat sehri GERCEK adres ucundan gelmeli");

            // ── CIFT-ANLAM KIRICI 1: teslimat sehri BILINMIYORSA KESIN TARIH YOK ──
            // "Her zaman genel ifade yaz" da bir cozum olurdu ama o zaman GERCEK sehri
            // bilen kullaniciya da tarih verilmezdi; iki dalin DA olmasi gerekiyor.
            var teslimat = FonksiyonGovdesi(s, "function deliveryHTML()");
            teslimat.Should().Contain("deliv_est_generic",
                "sehir bilinmiyorsa sehirsiz DURUST ifade yazilmali");
            teslimat.Should().Contain("deliv_est_city",
                "sehir BILINIYORSA gercek sehirle tahmin yazilmali (cift-anlam kirici)");
            var genelIndex = teslimat.IndexOf("deliv_est_generic", StringComparison.Ordinal);
            var rozetIndex = teslimat.IndexOf("deliv_fast", StringComparison.Ordinal);
            genelIndex.Should().BeLessThan(rozetIndex,
                "'Hizli Teslimat' rozeti sehirsiz daldan SONRA gelmeli - yani sehir yoksa CIZILMEMELI");

            // ── CIFT-ANLAM KIRICI 2: sehir KOSULSUZ 'Istanbul'a DUSMEMELI ────────
            var sehir = FonksiyonGovdesi(s, "function delivCity()");
            sehir.Should().NotContain("İstanbul",
                "sehir bulunamayinca KOSULSUZ bir sehre dusulmemeli - olculen once-durum buydu");
            sehir.Should().Contain("divisimaDelivCity",
                "sehir GERCEK adresten gelen koprulu degerden okunmali");

            // ── F-M6: yildiz KOSULLU ────────────────────────────────────────────
            var yildiz = FonksiyonGovdesi(s, "function pdRateHTML(rv)");
            yildiz.Should().Contain("rv.count>0",
                "puan satiri yorum sayisina KOSULLU olmali");
            yildiz.Should().Contain("rv_none",
                "yorum yokken 'Henuz degerlendirilmedi' yazilmali");
            // CIFT-ANLAM KIRICI: yorum VARSA gercek ortalama HALA gosterilmeli.
            yildiz.Should().Contain("rv.avg.toFixed(1)",
                "yorum varsa GERCEK ortalama gosterilmeli (yildizi tumden kaldirmak YANLIS duzeltmedir)");

            // ── VITRIN-FIX-2 KORUMALARI BOZULMADI ───────────────────────────────
            // P3 kart/cross-sell/karsilastirma yuzeyini tutuyor; o yuzey bu dalgada
            // DEGISMEDI ve yildiz kaynagi HALA sunucu alanlari.
            b.Should().Contain("Number(p.average_rating)",
                "yildiz kaynagi HALA sunucudan gelen average_rating olmali");
            b.Should().Contain("Number(p.review_count)",
                "yorum sayisi HALA sunucudan gelmeli");
        }

        // ── P8 (MFIX-2 / F-M1-H3 + MFIX-1 DEVRI): DETAY STOGU LISTEYI EZMEZ,
        //    SIPARIS SONRASI TAZELEME VAR ────────────────────────────────────────
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti R-M1H3 A/B:
        // ONCE urun 937'de liste 29 -> detay acilinca 35 (EZILDI); SONRA 29 -> 29.
        // Siparis tarafi: kurgu COD siparis 221 sonrasi vitrin 29 -> 28 ve DB de 28.
        [Fact]
        public void KAYNAK_SOZLESMESI_DetayStogu_Listeyi_Ezmez_ve_SiparisSonrasiTazeleme()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var s = YorumlariAyikla(Index);

            // ── ASIL SOZLESME 1: detay FIZIKSEL toplami listenin uzerine YAZMAZ ──
            // NOT (5. KONTROL YAKALADI - MFIX-1 dersinin TEKRARI): ilk yazimda bu assert
            // ESKI LITERAL BICIMI ariyordu. M-P8 mutasyonu ayni zarari BASKA BIR BICIMDE
            // yazinca (reduce ile toplam) pin KIRMIZI VERMEDI; mutasyon dosyaya inmisti ve
            // build temizdi, yani "uygulanmadi" DEGIL - PIN ZAYIFTI.
            // AYRICA regex'in ters bolu kacisi bu depoda YAZIM ZINCIRINDE KAYBOLDU (dorduncu
            // kez - CLAUDE.md dersi). Bu yuzden regex TUMDEN KALDIRILDI: bosluk ayiklanip
            // duz dizge araniyor. Kacis semantigi YOK, sessizce bozulamaz.
            var enrich = FonksiyonGovdesi(b, "async function enrichProduct(id)");
            var enrichSik = enrich.Replace(" ", "").Replace("\t", "");
            enrichSik.Should().NotContain("p.stock=",
                "detay zenginlestirmesi stok alanina HICBIR BICIMDE atama yapmamali - " +
                "listenin SATILABILIR degeri detayin FIZIKSEL toplamiyla ezilemez");

            // VAKUM KIRICI 1: govde GERCEKTEN okunmus olmali - bos string uzerinde
            // NotContain BEDAVA gecerdi.
            enrich.Trim().Length.Should().BeGreaterThan(200,
                "enrichProduct govdesi gercekten okunmus olmali");

            // VAKUM KIRICI 2: liste yolu stok degerini HALA yaziyor olmali - yoksa
            // "ezmiyor" iddiasi stok hic yazilmadigi icin bedava dogru olurdu.
            b.Should().Contain("stock: Number(p.total_stock)",
                "liste yolu total_stock'u HALA yaziyor olmali");

            // Beden haritasi HALA yaziliyor (detayin tek gercek katkisi) ...
            enrichSik.Should().Contain("p._ss=map",
                "beden bazi stok haritasi detaydan gelmeye devam etmeli");
            // ... ama LISTENIN bildirdigi bedenlerle SINIRLI.
            enrich.Should().Contain("listeBedenleri",
                "beden haritasi listenin bildirdigi bedenlerle sinirlanmali (tamamen rezerve beden gelmemeli)");

            // ── ASIL SOZLESME 2: siparis sonrasi tazeleme ───────────────────────
            b.Should().Contain("function katalogTazele()",
                "siparis sonrasi tazeleme yardimcisi tanimli olmali");
            Regex.Matches(b, @"\bkatalogTazele\s*\(\s*\)").Count.Should().BeGreaterThan(1,
                "tanim + cagri: en az iki gecis olmali (yalniz tanim = olu kod)");
            b.Should().Contain("if (ok) katalogTazele();",
                "tazeleme YALNIZ basarili sipariste kosmali");

            var tazele = FonksiyonGovdesi(b, "function katalogTazele()");
            tazele.Should().Contain("delete detailCache[k]",
                "detay onbellegi bosaltilmali - yoksa bayat stok geri gelir");
            tazele.Should().Contain("delete p._ss",
                "beden haritasi da bosaltilmali");
            tazele.Should().Contain("loadCatalog()",
                "katalog yeniden cekilmeli");

            // ── MFIX-1 DEVRI: mock checkout icerik fonksiyonlari SOKULDU ────────
            foreach (var ad in new[] { "coData", "coVal", "coFinish", "coStepBar",
                                       "coSummaryHTML", "addrItemHTML", "coStep1",
                                       "coStep2", "coStep3", "coAddrSum", "coPaySum",
                                       "coStepContent", "coSaveStep", "coValidateStep",
                                       "wireCheckout" })
            {
                Regex.Matches(s, @"\b" + Regex.Escape(ad) + @"\b").Count.Should().Be(0,
                    $"mock checkout parcasi '{ad}' sokulmus olmali");
            }

            // CIFT-ANLAM KIRICI: MFIX-1'in IKINCI SAVUNMA HATTI DURUYOR. Fonksiyonlari
            // silmek TEK BASINA yetmez - renderCheckout govdesi hala mock cizebilirdi.
            var mock = FonksiyonGovdesi(s, "function renderCheckout()");
            var mockSatirlari = mock.Split('\n');
            var yerTutucu = -1;
            for (var i = 0; i < mockSatirlari.Length; i++)
                if (mockSatirlari[i].Contains("Ödeme hazırlanıyor", StringComparison.Ordinal)) { yerTutucu = i; break; }
            yerTutucu.Should().BeGreaterThan(-1, "mock govdesi notr yer tutucuyu HALA yazmali");
            mockSatirlari[yerTutucu + 1].Trim().Should().Be("return;",
                "yer tutucudan HEMEN SONRAKI satir KOSULSUZ return olmali (MFIX-1 ikinci savunma hatti)");

            // ── ADDR/CARDS tohumlari BOSALTILDI (ciziciler SILINMEDI) ───────────
            Regex.IsMatch(s, @"var\s+ADDR\s*=\s*\[\s*\]\s*;").Should().BeTrue(
                "ADDR tohumu BOS olmali - defer yarisinda sahte adres gorunmemeli");
            Regex.IsMatch(s, @"var\s+CARDS\s*=\s*\[\s*\]\s*;").Should().BeTrue(
                "CARDS tohumu BOS olmali - defer yarisinda sahte kayitli kart gorunmemeli");
            // VAKUM KIRICI: ciziciler DURUYOR. Silinselerdi index.html'in kendi
            // renderAccount'u ReferenceError'a duserdi; tohum bosaltmak DURUST BOS
            // DURUMU gosterir - iddia "sahte veri yok", "ekran yok" DEGIL.
            s.Should().Contain("function accAddr()", "adres cizici YERINDE durmali");
            s.Should().Contain("function accCards()", "kart cizici YERINDE durmali");

            // ── F-M7: urun modali karartmaya tiklaninca kapanir ─────────────────
            Regex.IsMatch(s, @"modal\.addEventListener\('click',function\(e\)\{if\(e\.target===this\)closeModal\(\);\}\);")
                .Should().BeTrue("urun modali depodaki e.target===this kalibiyla kapanmali");
            // CIFT-ANLAM KIRICI: DIGER kapanis yollari DEGISMEDI.
            s.Should().Contain("function closeModal()", "closeModal yerinde durmali");
            s.Should().Contain("overlay.onclick", "overlay yolu da yerinde durmali");
        }

        // ── P9 (MFIX-3 / DEVIR-1 + DEVIR-2): UYDURMA OLAY IDDIASI URETILMEZ ──────
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR (depoda JS/DOM
        // kosucusu yok). Davranis kaniti R-SK A/B: ONCE canli yakalandi (t=108,6 sn ->
        // "Deniz Y. - Eskisehir" / "bu urunu satin aldi - 8 dk once"), SONRA 5+ dk gozlemde
        // SIFIR bildirim.
        //
        // OLCUT LITERAL BICIM DEGIL KUSUR SINIFI (MFIX-2'nin M-P8 dersi): "Math.random ile
        // uretilen kullanici-gorunur iddia" sinifi pinlenir; sosyal kaniti FARKLI bir
        // bicimde geri koyan bir mutasyon da kirilmali.
        [Fact]
        public void KAYNAK_SOZLESMESI_UydurmaOlayIddiasi_ve_SosyalKanit_Uretilmez()
        {
            var s = YorumlariAyikla(Index);
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // ── VAKUM KIRICI 1: tarama GERCEKTEN dosya okuyor ────────────────────
            s.Length.Should().BeGreaterThan(200000, "index.html okunmus olmali");
            Regex.Matches(s, @"\bfunction\b").Count.Should().BeGreaterThan(300,
                "tarama calisiyor olmali (negatif kontrol)");

            // ── ASIL SOZLESME: VITRINDE Math.random ile URETILEN HICBIR SEY YOK ──
            // Sosyal kanit uc ayri yerde Math.random kullaniyordu (havuz secimi, urun
            // secimi, tekrar araligi). Kusur SINIFI: index.html'de kullanici-gorunur
            // rastgelelik.
            Regex.Matches(s, @"Math\.random").Count.Should().Be(0,
                "index.html'de Math.random KALMAMALI - uydurma iddia uretmenin ana araciydi");

            // ── VAKUM KIRICI 2: rngOf HALA VAR (kapsam disi renk yuzeyi) ─────────
            // Duzeltme "tum rastgeleligi sil" DEGIL; silinseydi iddia BEDAVA dogru olurdu.
            // MFIX-3b PREMIS: `rngOf` SOKULDU (son cagirani variantsOf idi).
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali (vakum kirici)");

            // ── CIFT-ANLAM KIRICI: api-bridge'in MESRU rastgeleligi DURMALI ──────
            // request_id (idempotency anahtari) Math.random kullanir ve bu DOGRUDUR;
            // "her rastgeleligi kaldir" YANLIS duzeltmedir.
            b.Should().Contain("Math.random",
                "api-bridge'teki request_id yedegi MESRU - kaldirilmamali (cift-anlam kirici)");
            b.Should().Contain("request_id",
                "idempotency anahtari yerinde durmali");

            // ── SOSYAL KANIT ARTIKLARI: markup, CSS, JS, i18n ────────────────────
            foreach (var ad in new[] { "socialProof", "sp-toast", "sp-verified", "spImg",
                                       "spProd", "spName", "spMeta", "spX", "dvs_sp_off",
                                       "sp_bought", "sp_ago", "sp_from", "contextPool" })
            {
                Regex.Matches(s, Regex.Escape(ad)).Count.Should().Be(0,
                    "sosyal kanit artigi '" + ad + "' kaynakta HIC gecmemeli (yorumlar ayiklanmis halde)");
            }

            // ── DEVIR-2: MOCK_ORDERS TOHUMU BOS ─────────────────────────────────
            // Uydurma siparis numarasi/tarih/durum tasiyan tohum bosaltildi; CIZICI DURUYOR.
            Regex.IsMatch(s, @"var\s+MOCK_ORDERS\s*=\s*\[\s*\]\s*;").Should().BeTrue(
                "MOCK_ORDERS tohumu BOS olmali (ADDR/CARDS tedavisi)");
            Regex.Matches(s, @"DVS-\d{8}").Count.Should().Be(0,
                "uydurma siparis numarasi kaynakta kalmamali");
            // CIFT-ANLAM KIRICI: cizici SILINMEDI (silmek renderAccount'u ReferenceError'a
            // dusururdu) ve bos tohumda DURUST bir bos durum gosteriyor.
            s.Should().Contain("function accOrders()", "siparis cizici YERINDE durmali");
            s.Should().Contain("function openReturn(", "iade cizici YERINDE durmali");
            var accOrders = FonksiyonGovdesi(s, "function accOrders()");
            accOrders.Should().Contain("MOCK_ORDERS.length",
                "bos tohumda DURUST bos durum gosterilmeli");
            accOrders.Should().Contain("orders_empty",
                "bos durum metni sozlukten gelmeli");
        }

        // ── P10 (MFIX-3 / F-M4 + F-M5): MISAFIR SEPETI KALICI, FAVORILER HESABA OZGU ──
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti A/B olcumleri:
        //   F-M4 ONCE (ayirt edici deney): dvs_cart'ta mock-id 2 + gercek-id 955 ->
        //     yenilemeden sonra YALNIZ id 2 kaldi, 955 SILINDI ve dvs_cart yeniden yazildi.
        //   F-M5 ONCE: misafir kalbi cihaz-geneli anahtara yazdi (wishlist_items TOPLAM=0),
        //     ardindan giris yapan hesap o favorileri DEVRALDI.
        [Fact]
        public void KAYNAK_SOZLESMESI_MisafirSepeti_KatalogSonrasiYuklenir_ve_Favoriler_SunucudanHesabaOzgu()
        {
            var s = YorumlariAyikla(Index);
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // ── F-M4 (1): geri yukleme AYRI fonksiyonda ve MOCK KAPISI YOK ───────
            var geriYukle = FonksiyonGovdesi(s, "function sepetiGeriYukle()");
            geriYukle.Should().Contain("dvs_cart", "geri yukleme yerel depodan okumali");
            geriYukle.Replace(" ", "").Should().NotContain("byId(it.id)&&",
                "katalog-oncesi byId kapisi KALMAMALI - gercek urunleri eliyordu");
            // VAKUM KIRICI: adet dogrulamasi HALA duruyor (kapinin tamami silinmedi).
            geriYukle.Should().Contain("isFinite", "adet dogrulamasi korunmali");

            // ── F-M4 (2): renderCart SILMEZ, yalniz CIZMEZ ──────────────────────
            var renderCart = FonksiyonGovdesi(s, "function renderCart()");
            renderCart.Replace(" ", "").Should().NotContain("byId(it.id);if(!p){cart.delete(k)",
                "urunu bulunamayan kalem SILINMEMELI - katalogda olmayan kalem KORUNUR");
            // CIFT-ANLAM KIRICI: KULLANICI silme yollari DURMALI (hepsini kaldirmak yanlis olurdu).
            renderCart.Should().Contain("byId(it.id)", "cizim icin urun cozumu HALA yapilmali");
            s.Should().Contain("cart.delete(_rk)", "kullanicinin 'kaldir' yolu DURMALI");

            // ── F-M4 (3): katalogtan SONRA tamamlama ────────────────────────────
            b.Should().Contain("function sepetUrunleriniTamamla",
                "katalogda olmayan sepet urunleri tamamlanmali");
            var init = FonksiyonGovdesi(b, "async function init()");
            var iKatalog = init.IndexOf("await loadCatalog()", StringComparison.Ordinal);
            var iTamamla = init.IndexOf("sepetUrunleriniTamamla", StringComparison.Ordinal);
            var iGeri = init.IndexOf("sepetiGeriYukle", StringComparison.Ordinal);
            iKatalog.Should().BeGreaterThan(-1, "init katalogu yuklemeli");
            iTamamla.Should().BeGreaterThan(iKatalog,
                "sepet tamamlamasi KATALOGDAN SONRA gelmeli");
            iGeri.Should().BeGreaterThan(iKatalog,
                "sepet geri yuklemesi de KATALOGDAN SONRA bir kez daha kosmali");

            // ── F-M5 (1): dvs_favs NE OKUNUR NE YAZILIR ─────────────────────────
            Regex.Matches(s, "dvs_favs").Count.Should().Be(0,
                "cihaz-geneli favori anahtari kaynakta HIC gecmemeli (yorumlar ayiklanmis halde)");
            // CIFT-ANLAM KIRICI: fonksiyon SILINMEDI (cagiranlari ReferenceError'a dusururdu).
            s.Should().Contain("function saveFavs()", "saveFavs YERINDE durmali");
            s.Should().Contain("function toggleFav(", "toggleFav YERINDE durmali");

            // ── F-M5 (2): misafirde YEREL YAZMA YOK, GORUNUR yonlendirme VAR ────
            var wireFav = FonksiyonGovdesi(b, "function wireFavoriler()");
            wireFav.Should().Contain("api.isLoggedIn()", "misafir/uye ayrimi yapilmali");
            wireFav.Should().Contain("fav_login", "misafire GORUNUR yonlendirme metni verilmeli");
            wireFav.Should().Contain("#/giris", "MEVCUT giris akisina yonlendirilmeli");
            // Sunucu sozlesmesi KAYNAKTAN okundu: Toggle(int productId) - SORGU DIZESI.
            // MFIX-3b PREMIS: uc literali api-client.wishlist.toggle-a TASINDI (TEK SOZLESME).
            // wireFavoriler artik o uyeyi cagiriyor; SORGU DIZESI sozlesmesi P16-da pinli.
            wireFav.Should().Contain("api.wishlist.toggle(", "sunucu ucu api-client uyesi uzerinden cagrilmali");
            // CIFT-ANLAM KIRICI: yerel durum ancak SUNUCU ONAYLADIKTAN sonra degismeli.
            var iPost = wireFav.IndexOf("api.wishlist.toggle(", StringComparison.Ordinal);
            var iOrig = wireFav.IndexOf("orig.call(window, id)", StringComparison.Ordinal);
            iOrig.Should().BeGreaterThan(iPost,
                "yerel guncelleme sunucu cagrisindan SONRA gelmeli (ekran sunucudan ayrisamaz)");

            // ── F-M5 (3): liste SUNUCUDAN, cikista GORUNUM temizlenir ──────────
            b.Should().Contain("api.wishlist.get()", "favori listesi sunucudan gelmeli");
            b.Should().Contain("function favorileriTemizle", "cikista gorunum temizlenmeli");
            var logout = FonksiyonGovdesi(b, "async logout()");
            logout.Should().Contain("favorileriTemizle", "cikista favori gorunumu temizlenmeli");
            // CIFT-ANLAM KIRICI: cikista SEPETE DOKUNULMAZ (kapsam karari).
            logout.Should().NotContain("dvs_cart", "cikista sepet KORUNMALI");
        }

        // ── P11 (MFIX-3): MFIX-2 REGRESYON SINIFI + TEK KAYNAK OLCUTLER ──────────
        // GEREKCE: MFIX-2'nin mock-checkout sokumu, `wireCheckout` ile birlikte KOMSU IKI
        // FONKSIYONU DA goturdu (setAnnShip, refreshPrices) ama CAGRI YERLERI kaldi.
        // CANLI OLCULDU (MFIX-3): applyI18n() / setLang() / setCur() UCU DE istisna
        // firlatiyordu - yani DIL DEGISTIRME BOZUKTU ve duyuru seridi BOS kaliyordu.
        // Hicbir pin bunu yakalamiyordu; bu pin O SINIFI kapatir.
        //
        // KAPSAM SINIRI (durust): tarama, cerceve GIRIS NOKTALARININ govdeleriyle
        // sinirlidir - genel bir "tanimsiz global" analizi DEGILDIR. Bu dort fonksiyon
        // secildi cunku regresyonun gectigi yol tam olarak buydu ve icerdikleri cagri
        // kumesi OLCULDU (disarida yalniz JS anahtar sozcukleri kaldi).
        [Fact]
        public void KAYNAK_SOZLESMESI_CerceveGirisNoktalari_TANIMSIZ_FONKSIYON_CAGIRMAZ_ve_Olcutler_TEK_KAYNAK()
        {
            var s = YorumlariAyikla(Index);
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // Tanimli fonksiyon adlari (index.html)
            var tanimli = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(s, @"function\s+([A-Za-z_$][A-Za-z0-9_$]*)\s*\("))
                tanimli.Add(m.Groups[1].Value);
            // VAKUM KIRICI: tarama gercekten calisti.
            tanimli.Count.Should().BeGreaterThan(150, "index.html'de coklu fonksiyon tanimi bulunmali");

            // JS anahtar sozcukleri (cagri gibi gorunurler). Liste OLCULDU: dort govdede
            // bunlarin disinda cerceve-disi tanimlayici YOK.
            var anahtarSozcuk = new HashSet<string>(StringComparer.Ordinal)
            { "function", "if", "for", "while", "switch", "catch", "return", "typeof", "new", "do" };

            var girisNoktalari = new[]
            {
                "function applyI18n()", "function setLang(l)",
                "function setCur(code)", "function refreshPrices()"
            };
            foreach (var imza in girisNoktalari)
            {
                var govde = FonksiyonGovdesi(s, imza);
                govde.Length.Should().BeGreaterThan(40, imza + " govdesi bos okunmus olamaz");
                foreach (Match c in Regex.Matches(govde, @"(?<![.$\w])([A-Za-z_$][A-Za-z0-9_$]*)\s*\("))
                {
                    var ad = c.Groups[1].Value;
                    if (anahtarSozcuk.Contains(ad)) continue;
                    tanimli.Contains(ad).Should().BeTrue(
                        "'" + imza + "' icinde cagrilan '" + ad + "' index.html'de TANIMLI olmali - " +
                        "MFIX-2'de tam bu sinif bir regresyon uretti (setAnnShip / refreshPrices)");
                }
            }

            // ── DEVIR-3: ODEME BASARI OLCUTU TEK KAYNAK ────────────────────────
            b.Should().Contain("function odemeBasariliMi(",
                "basari olcutu TEK fonksiyonda olmali");
            Regex.Matches(b, @"odemeBasariliMi\(").Count.Should().BeGreaterThan(1,
                "olcut tanimlanmis VE kullanilmis olmali");
            // Baslik ANAHTARI da tek kaynaktan: ekran ve sekme AYNI metni gostermeli.
            b.Should().Contain("function odemeSonucBaslikAnahtari(",
                "baslik anahtari TEK fonksiyonda olmali");
            Regex.Matches(b, @"odemeSonucBaslikAnahtari\(").Count.Should().BeGreaterThan(2,
                "baslik anahtari HEM ekran HEM sekme tarafinda kullanilmali (tanim + iki cagri)");
            // CIFT-ANLAM KIRICI: eski, yalniz "success" arayan bicim GERI GELEMEZ.
            b.Should().NotContain("indexOf(\"status=success\")",
                "sekme basligi artik yalniz 'success' aramamali - kapida odeme de BASARIDIR");

            // ── F-M3g: resendVerification SORGU DIZESI kullanir ────────────────
            var c2 = Oku("frontend/api-client.js");
            c2.Replace(" ", "").Should().Contain(
                "resendVerification(email){returnapi._post(\"/api/auth/resend-verification\"+api._qs",
                "uc [FromQuery] bekliyor - govde ile cagrildiginda CANLI 400 olculdu");
            // VAKUM KIRICI: kardes uc (verifyEmail) ZATEN ayni kalibi kullaniyor.
            c2.Should().Contain("/api/auth/verify-email\" + api._qs",
                "kalip depoda zaten var (vakum kirici)");

            // ── F-M2: api-bridge'in KULLANDIGI HER ANAHTAR SOZLUKTE OLMALI ─────
            // Bilincli karar: ceviri() cagrilarina YEDEK METIN konmadi; yanlis/eksik bir
            // anahtar ekranda HAM ANAHTAR gosterirdi. Bunu calisma anina birakmak yerine
            // KIRMIZI BIR TESTE bagladik.
            var tBlok = SozlukBlogu(Index, "var T={", "var AR={");
            var arBlok = SozlukBlogu(Index, "var AR={", "function t(k)");
            var tAnahtar = SozlukAnahtarlari(tBlok, @"\[");
            // MFIX-3b PREMIS GUNCELLEMESI (merkez onayina): AR degerleri artik TEK ya da
            // CIFT tirnakli olabilir. Gerekce OLCUMDUR: yeni cevirilerin bir kismi apostrof
            // iceriyor (or. "provider's") ve tek-tirnakli uretimde apostrof kacisi arac
            // zincirinde KAYBOLUP sozlugu JS sozdizimi hatasina dusurdu (konsolda
            // "SyntaxError: Unexpected identifier 's'" ile yakalandi). TSV'de cift tirnak
            // KARAKTERI OLMADIGI olculdugu icin cift-tirnakli uretim KACIS GEREKTIRMEZ.
            // Pinin OLCTUGU SEY DEGISMEDI: "AR sozlugu T ile TAM ortusmeli".
            var arAnahtar = SozlukAnahtarlari(arBlok, "[\"']");
            tAnahtar.Count.Should().BeGreaterThan(500, "T sozlugu okunmus olmali (vakum kirici)");
            arAnahtar.Count.Should().BeGreaterThan(500, "AR sozlugu okunmus olmali (vakum kirici)");

            var kullanilan = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(b, @"ceviri\(([^;]{0,240}?)\)"))
                foreach (Match q in Regex.Matches(m.Groups[1].Value, "\"([a-z][a-z0-9_]*)\""))
                    kullanilan.Add(q.Groups[1].Value);
            foreach (var blokAdi in new[] { "var SIPARIS_DURUM_ANAHTARI", "var DURUM_ANAHTAR", "var IADE_ANAHTAR" })
            {
                var blok = FonksiyonGovdesi(b, blokAdi);   // susli parantez govdesi
                foreach (Match q in Regex.Matches(blok, ":\\s*\"([a-z][a-z0-9_]*)\""))
                    kullanilan.Add(q.Groups[1].Value);
            }
            foreach (Match q in Regex.Matches(b, "\\[\"[a-z]+\",\\s*\"([a-z][a-z0-9_]*)\"\\]"))
                kullanilan.Add(q.Groups[1].Value);
            // `ceviri(status === "cod" ? ...)` icindeki "cod" bir SOZLUK ANAHTARI DEGIL,
            // sunucudan gelen durum degeridir; taramanin TEK istisnasi budur.
            kullanilan.Remove("cod");

            kullanilan.Count.Should().BeGreaterThan(30,
                "api-bridge coklu sozluk anahtari kullaniyor olmali (vakum kirici)");
            foreach (var k in kullanilan)
            {
                tAnahtar.Contains(k).Should().BeTrue(
                    "api-bridge'in kullandigi '" + k + "' anahtari T sozlugunde OLMALI");
                arAnahtar.Contains(k).Should().BeTrue(
                    "api-bridge'in kullandigi '" + k + "' anahtari AR sozlugunde OLMALI");
            }

            // ── F-M2 EK: AR sozlugu T ile TAM ORTUSMELI ────────────────────────
            // MTUR'da olculen iki eksik anahtar ('sort_price-asc' / 'sort_price-desc')
            // AD-TABANLI taramalarda TIRE yuzunden gozden kaciyordu; burada tire de kapsamda.
            var eksik = new List<string>();
            foreach (var k in tAnahtar) if (!arAnahtar.Contains(k)) eksik.Add(k);
            eksik.Should().BeEmpty("AR sozlugu T ile TAM ortusmeli (tireli anahtarlar DAHIL)");
        }

        // Sozluk blogunu (basi/sonu isaretleriyle) cikarir.
        private static string SozlukBlogu(string kaynak, string bas, string son)
        {
            var i = kaynak.IndexOf(bas, StringComparison.Ordinal);
            i.Should().BeGreaterThan(-1, "sozluk basi bulunmali: " + bas);
            var j = kaynak.IndexOf(son, i, StringComparison.Ordinal);
            j.Should().BeGreaterThan(i, "sozluk sonu bulunmali: " + son);
            return kaynak.Substring(i, j - i);
        }

        // Sozluk anahtarlari: `ad:` ve `'tireli-ad':` bicimlerinin IKISI DE.
        // Multiline SART: sozluk girdileri SATIR BASINDA da baslayabiliyor (yalniz `,`/`{`
        // ardindan degil) - ilk yazimda bu unutuldu ve pin YANLIS kirmizi verdi.
        // Yorumlar ONCE ayiklanir: aciklama metnindeki "kelime:" kaliplari anahtar sanilirdi.
        // `degerAcici`: T sozlugunde deger DIZI ile ("[") , AR'da TEK TIRNAK ile baslar.
        // Bu SART, cunku deger METINLERI de "kelime:" kalibi tasiyabiliyor - ornegin
        // `sort_prefix:['Sirala:','Sort:']` icindeki 'Sort:' anahtar sanilip pin YANLIS
        // kirmizi verdi (ilk kosumda birebir yasandi: Sort/Colour/Size/Currency).
        private static HashSet<string> SozlukAnahtarlari(string blok, string degerAcici)
        {
            var temiz = YorumlariAyikla(blok);
            var kume = new HashSet<string>(StringComparer.Ordinal);
            var desen = @"(?:^|[,{])\s*'?([A-Za-z_][A-Za-z0-9_\-]*)'?\s*:\s*" + degerAcici;
            foreach (Match m in Regex.Matches(temiz, desen, RegexOptions.Multiline))
                kume.Add(m.Groups[1].Value);
            return kume;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // MFIX-3b / P15: UYDURMA RENK ve SAHTE ACILIYET URETILMEZ
        //
        // OLCUT LITERAL BICIM DEGIL KUSUR SINIFIDIR (MFIX-2 dersi: M-P8'in ilk turunda
        // ayni kusur FARKLI bicimde geri konunca pin sessiz kalmisti).
        //   (a) URETIM: renk varyanti bir PRNG'den turetilemez - urunun gercek renk
        //       varyanti verisi YOK (uc yalniz tek bir color_hex donuyor).
        //   (b) CIZIM : renk secim yuzeyi (swatch/cip) DOM'a hic girmez.
        //   (c) ACILIYET: gece yarisina (ya da baska bir sabit ana) sayan bir geri sayim
        //       KURULAMAZ - o anda biten bir sey YOK (sale_end mekanizmasi yok).
        //       Olcut "setInterval" DEGIL, ZAMANLAYICI + GERI SAYIM HEDEFI birlesimidir;
        //       boylece sayaci baska bir duzenekle (requestAnimationFrame, setTimeout
        //       dongusu, Date farki) geri koymak da yakalanir.
        // ─────────────────────────────────────────────────────────────────────────
        [Fact]
        public void KAYNAK_SOZLESMESI_UydurmaRenk_ve_SahteAciliyet_Uretilmez()
        {
            var s = YorumlariAyikla(Index);

            // VAKUM KIRICI: dosya gercekten okundu ve PRNG yardimcisi HALA duruyor
            // (kapsam disi yuzeylerde kullaniliyor) - yani tarama "her sey gitti"
            // diye bedava yesil olamaz.
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali");
            s.Should().NotContain("function rngOf(",
                "PRNG ureticisinin KENDISI de SOKULDU - son cagirani variantsOf idi");

            // (a) URETIM: renk varyanti ureticisi YOK
            s.Should().NotContain("function variantsOf",
                "uydurma renk varyanti ureticisi SOKULDU");
            Regex.Matches(s, @"\bvariantsOf\s*\(").Count.Should().Be(0,
                "variantsOf hicbir yerden CAGRILMAMALI");
            Regex.Matches(s, @"\b_vr\b").Count.Should().Be(0,
                "variantsOf'un onbellek alani (_vr) da kalmamali");

            // (b) CIZIM: renk secim yuzeyi DOM'a girmiyor
            foreach (var isaret in new[] { "pd-swatch\"", "id=\"pdSwatches\"", "class=\"card-cols\"", "data-cdot" })
                s.Should().NotContain(isaret,
                    "renk secim yuzeyi '" + isaret + "' DOM'a URETILMEMELI");
            s.Should().NotContain("function applyColor",
                "renk uygulayici SOKULDU");

            // (c) ACILIYET: geri sayim hedefi + zamanlayici birlesimi YOK
            var zamanlayici = new Regex(@"set(?:Interval|Timeout)\s*\(|requestAnimationFrame\s*\(");
            foreach (var imza in new[] { "function startDealCountdown", "function stopDealCountdown" })
                s.Should().NotContain(imza, imza + " SOKULDU");
            foreach (var hedef in new[] { "setHours(24", "setHours( 24", "dealClock", "camp-clock", "id=\"cdH\"", "id=\"cdM\"", "id=\"cdS\"" })
                s.Should().NotContain(hedef,
                    "sahte aciliyet isareti '" + hedef + "' kalmamali");
            // Sure VAADI metinleri de gitti (sozlukte de olmamali)
            foreach (var metin in new[] { "camp_ends", "camp_eyebrow", "deal_ends" })
                s.Should().NotContain(metin, "'" + metin + "' sure vaadi anahtari SOKULDU");
            // CIFT-ANLAM KIRICI: INDIRIM ROZETI ve ustu-cizili fiyat KALMALI - bunlar
            // GERCEK VERIDIR (old_price / sale_price). "hepsini sil" YANLIS duzeltmedir.
            s.Should().Contain("deal-strip", "indirim seridi (rozet) KALMALI");
            s.Should().Contain("function discPct(", "indirim yuzdesi hesabi KALMALI");
            zamanlayici.IsMatch(s).Should().BeTrue(
                "zamanlayicilar genel olarak HALA var (slider vb.) - olcut zamanlayici degil, GERI SAYIM HEDEFIDIR");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // MFIX-3b / P16: TOAST TIP TASIR + WISHLIST TOGGLE SORGU DIZESI
        //   (a) toast(msg, tip) imzasi ve VARSAYILAN "info" (T1 ekraninda olculdu:
        //       tipsiz cagrinin onay isareti basmasi sinif olarak olu bir kusurdur).
        //   (b) HICBIR eylem toasti TIPSIZ cagrilmaz.
        //   (c) api-client.wishlist.toggle SORGU DIZESI kullanir ve api-bridge kendi
        //       kopyasini DEGIL o uyeyi cagirir (TEK SOZLESME).
        // ─────────────────────────────────────────────────────────────────────────
        [Fact]
        public void KAYNAK_SOZLESMESI_Toast_TipTasir_ve_WishlistToggle_QueryString()
        {
            var s = YorumlariAyikla(Index);
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var c = YorumlariAyikla(Oku("frontend/api-client.js"));

            // (a) imza + varsayilan
            s.Should().Contain("function toast(msg,tip)", "toast TIP parametresi almali");
            s.Replace(" ", "").Should().Contain("?tip:'info'",
                "tip verilmediginde VARSAYILAN 'info' olmali - 'ok' DEGIL");
            foreach (var tip in new[] { "ok", "err", "info" })
                s.Should().Contain("_TOAST_IKON", "tip -> ikon eslemesi TEK yerde olmali");

            // (b) TIPSIZ eylem toasti YOK. Tarama index.html VE api-bridge.
            // VAKUM KIRICI: taranan cagri sayisi anlamli olmali.
            var cagriDeseni = new Regex(@"(?<![.\w$])(?:toast|notify)\s*\(");
            int toplam = 0, tipsiz = 0;
            var tipsizOrnek = new List<string>();
            foreach (var kaynak in new[] { s, b })
            {
                foreach (Match m in cagriDeseni.Matches(kaynak))
                {
                    var acilis = kaynak.IndexOf('(', m.Index);
                    var kapanis = ParantezKapanisi(kaynak, acilis);
                    if (kapanis < 0) continue;
                    var arg = kaynak.Substring(acilis + 1, kapanis - acilis - 1);
                    // TANIM satirlari (function toast(msg,tip) / function notify(msg, tip)) haric
                    if (Regex.IsMatch(kaynak.Substring(Math.Max(0, m.Index - 12), Math.Min(12, m.Index)), @"function\s*$")) continue;
                    toplam++;
                    if (!Regex.IsMatch(arg, @"[,]\s*(?:'|\"")(?:ok|err|info)(?:'|\"")\s*$") &&
                        !Regex.IsMatch(arg, @",\s*tip\s*$"))
                    { tipsiz++; if (tipsizOrnek.Count < 5) tipsizOrnek.Add(arg.Length > 70 ? arg.Substring(0, 70) : arg); }
                }
            }
            toplam.Should().BeGreaterThan(50, "toast/notify cagrilari taranmis olmali (vakum kirici)");
            tipsiz.Should().Be(0,
                "TIPSIZ toast/notify cagrisi kalmamali. Ornekler: " + string.Join(" || ", tipsizOrnek));

            // (c) wishlist sozlesmesi
            c.Replace(" ", "").Should().Contain(
                "toggle(productId){returnapi._post(\"/api/wishlist/toggle\"+api._qs",
                "uc Toggle(int productId) - [FromBody] YOK; govde bicimi CANLI 500 uretiyordu");
            b.Should().Contain("api.wishlist.toggle(",
                "api-bridge kendi el yazmasi yerine api-client uyesini cagirmali (TEK SOZLESME)");
            Regex.Matches(b, @"_post\(\s*""/api/wishlist/toggle").Count.Should().Be(0,
                "api-bridge'teki GECICI kopya KALDIRILMIS olmali");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // MFIX-3b / P17: TARIH BICIMI LOCALE BAGLI + DIL DEGISIMI SEPET YAZMAYI TETIKLEMEZ
        //   (a) Bicimleyiciler SABIT 'tr-TR' tasimaz; hepsi TEK KAYNAKTAN (dvsLocale).
        //   (b) Salt-cizim yolu (dil/para/sekme) sunucuya yazma ZAMANLAMAZ - olcut
        //       sepetin IMZASIDIR. Kabul turunda olculen zarar: dil degisimi sepeti
        //       yeniden yaziyor, stok dustuyse 400 aliyor ve kullaniciya YANLIS
        //       TESHIS ("internet baglantini kontrol et") gosteriliyordu.
        //   (c) Hata metni GERCEK sebebi soyler.
        // ─────────────────────────────────────────────────────────────────────────
        [Fact]
        public void KAYNAK_SOZLESMESI_TarihBicimi_LocaleBagli_ve_DilDegisimi_SepetYazmayi_Tetiklemez()
        {
            var s = YorumlariAyikla(Index);
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // (a) TEK KAYNAK
            s.Should().Contain("function dvsLocale()", "locale eslemesi TEK fonksiyonda olmali");
            s.Should().Contain("window.dvsLocale=dvsLocale",
                "api-bridge'in erisebilmesi icin disa acilmali");
            // Bicimleyiciler artik SABIT tr-TR tasimaz.
            foreach (var imza in new[] { "function tl(n)", "function rvTarih(s)" })
            {
                var govde = FonksiyonGovdesi(s, imza);
                govde.Length.Should().BeGreaterThan(30, imza + " govdesi bos okunmus olamaz");
                govde.Should().NotContain("'tr-TR'", imza + " SABIT tr-TR TASIMAMALI");
                govde.Should().Contain("dvsLocale()", imza + " locale'i TEK KAYNAKTAN almali");
            }
            // api-bridge tarafinda da SABIT tr-TR yok; tr-TR yalniz YEDEK olarak gecebilir.
            foreach (Match m in Regex.Matches(b, "\"tr-TR\""))
            {
                var bas = Math.Max(0, m.Index - 90);
                var oncesi = b.Substring(bas, m.Index - bas);
                oncesi.Should().Contain("dvsLocale",
                    "api-bridge'te gecen her tr-TR, dvsLocale YOKKEN devreye giren YEDEK olmali");
            }
            // CIFT-ANLAM KIRICI: arama normalizasyonu bir KIMLIK islemidir (CLAUDE.md 6c)
            // ve KULTURLU kalmalidir - "hepsini locale'e bagla" YANLIS duzeltmedir.
            s.Should().Contain("toLocaleLowerCase('tr')",
                "arama normalizasyonu KIMLIK islemidir, DEGISMEMELI");

            // (b) salt-cizim yazma tetiklemez
            b.Should().Contain("function sepetImzasi()", "sepet imzasi TEK yerde uretilmeli");
            b.Replace(" ", "").Should().Contain("sepetImzasi()===sonSenkronImzasi",
                "salt-cizim kapisi imza karsilastirmasina dayanmali");
            // VAKUM KIRICI: senkronun KENDISI HALA var (kapi 'her seyi kapat' degil).
            b.Should().Contain("syncTimer = setTimeout(syncCartToServer",
                "gercek degisiklikte senkron HALA zamanlanmali");
            // ILK SENKRON (birlestirme) kapidan MUAF olmali - yoksa sunucu sepeti okunamaz.
            b.Replace(" ", "").Should().Contain("if(ilkSenkronYapildi&&sepetImzasi()",
                "ilk senkron (birlestirme) kapidan MUAF olmali");

            // (c) durust hata metni
            b.Should().Contain("err_cart_sync_reason",
                "sunucu yanit verdiyse ONUN sebebi gosterilmeli");
            b.Should().Contain("err_cart_offline",
                "yalnizca ag hatasinda baglanti metni gosterilmeli");
            b.Should().NotContain("ceviri(\"err_cart_sync\")",
                "sabit 'internet' teshisi veren eski anahtar KALDIRILMIS olmali");
        }

        // Bir acilis parantezinin ESLESEN kapanisini bulur (tirnak farkindaligi ile).
        // Toast argumanlari icinde parantez ve tirnak IC ICE gecebiliyor; duz arama
        // yanlis kapanis bulurdu.
        private static int ParantezKapanisi(string s, int acilisIndeksi)
        {
            int derinlik = 0; char tirnak = '\0';
            for (int i = acilisIndeksi; i < s.Length; i++)
            {
                char ch = s[i];
                if (tirnak != '\0') { if (ch == '\\') { i++; continue; } if (ch == tirnak) tirnak = '\0'; continue; }
                if (ch == '\'' || ch == '"' || ch == '`') { tirnak = ch; continue; }
                if (ch == '(') derinlik++;
                else if (ch == ')') { derinlik--; if (derinlik == 0) return i; }
            }
            return -1;
        }
    }
}
