using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // KURAL-UYUM DENETIMI (MFIX-3b): rastgelelik kaynagi olcutu ARTIK AD DEGIL SINIF.
        // Onceki hal tek bir yardimci ADINA (`rngOf`) bakiyordu; o yardimci tumden
        // silinince assert HICBIR KOSULDA kirilamaz hale gelmisti (bolum 6 vakum yasagi).
        // Desen: bilinen tum rastgelelik kaynaklarini VE tohumlu-uretici kalibini kapsar.
        private static readonly Regex RASTGELELIK =
            new(@"Math\.random|crypto\.getRandomValues|\brngOf\b|\bmulberry32\b|\bxorshift\b|\bseed\s*=",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            // GF-2b/K5: panelin JS'i `frontend/admin.js`e tasindi. Dosya listeye EKLENDI -
            // eklenmeseydi bu SINIF DUZEYI tarama panelin TUM kodunu sessizce kapsam
            // disinda birakirdi (kapsam kaybi, yanlis yesil).
            var dosyalar = new[] { "frontend/index.html", "frontend/api-bridge.js", "frontend/admin.html", "frontend/admin.js" };
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
            // MFIX-3b + KURAL-UYUM DENETIMI: eski vakum kirici ("rngOf HALA kullaniliyor")
            // BU DALGADA GECERSIZ KALDI - rngOf tumden SOKULDU, dolayisiyla ona bakan
            // NotContain artik HICBIR KOSULDA kirilamazdi (bolum 6 vakum yasagi).
            // OLCUT ARTIK KUSUR SINIFI: govde HICBIR rastgelelik kaynagindan beslenmez.
            // Desen ADI DEGIL SINIFI tarar - yarin baska bir PRNG adi gelse de yakalar.
            // VAKUM KIRICI: asagidaki POZITIF assertler (gercek veri kaynagi) govdenin
            // okundugunu ve anlamli oldugunu ZATEN kanitliyor.
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali");

            // ASIL SOZLESME: beden stogu tohumlu rastgelelikten TUREMEZ.
            govde.Should().NotMatchRegex(RASTGELELIK,
                "beden bazli stok UYDURULMAZ; bilinmiyorsa BOS harita donulur ve urun toplami " +
                "sunucunun verdigi total_stock'tan (p.stock) okunur - HICBIR rastgelelik kaynagi");

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
            // MFIX-3b + KURAL-UYUM DENETIMI: eski vakum kirici ("rngOf HALA kullaniliyor")
            // BU DALGADA GECERSIZ KALDI - rngOf tumden SOKULDU, dolayisiyla ona bakan
            // NotContain artik HICBIR KOSULDA kirilamazdi (bolum 6 vakum yasagi).
            // OLCUT ARTIK KUSUR SINIFI: govde HICBIR rastgelelik kaynagindan beslenmez.
            // Desen ADI DEGIL SINIFI tarar - yarin baska bir PRNG adi gelse de yakalar.
            // VAKUM KIRICI: asagidaki POZITIF assertler (gercek veri kaynagi) govdenin
            // okundugunu ve anlamli oldugunu ZATEN kanitliyor.
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali");

            // VAKUM KIRICI 2: tarama gercekten bir govde okumus olmali.
            var govde = FonksiyonGovdesi(s, "function reviewsOf(p)");
            govde.Length.Should().BeGreaterThan(120, "reviewsOf govdesi bos okunmus olamaz");

            // ASIL SOZLESME: yorum verisi tohumlu rastgelelikten TUREMEZ.
            govde.Should().NotMatchRegex(RASTGELELIK,
                "yildiz ve yorum sayisi bir TICARI BEYANDIR - uydurulamaz (hicbir rastgelelik kaynagi)");

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
            // ══ GF-2b / K4 - TAZELEYICI YENIDEN ADLANDIRILDI ══════════════════════════
            // Olcut artik yalniz SEPET degil, CHECKOUT NIYETI: sepet + adres + kupon +
            // bakiye kullanimi + odeme yontemi (misafir yolunda ayrica e-posta). Sunucu
            // GF-3/K12 replay olcutu de bu eksende; ad ESKI olcutu anlatiyordu ve YANILTICIYDI.
            // Bu pinin OLCTUGU SEY DEGISMEDI: tazeleyici TANIMLI ve CAGRILMIS olmali.
            // Daha SIKI bir karsiligi `GuvenlikFix2bSozlesmeTests.GF2B_K4_NIYET_IMZASI_...`
            // icinde duruyor - orada cagri sayisi TAM 2 olarak pinli (uye + misafir).
            b.Should().Contain("function checkoutIstekIdNiyeteGoreTazele()", "niyet tazeleyici tanimli olmali");
            Regex.Matches(b, @"checkoutIstekIdNiyeteGoreTazele\s*\(\s*\)").Count.Should().BeGreaterThan(1,
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
            // MFIX-3b + KURAL-UYUM DENETIMI: eski vakum kirici ("rngOf HALA kullaniliyor")
            // BU DALGADA GECERSIZ KALDI - rngOf tumden SOKULDU, dolayisiyla ona bakan
            // NotContain artik HICBIR KOSULDA kirilamazdi (bolum 6 vakum yasagi).
            // OLCUT ARTIK KUSUR SINIFI: govde HICBIR rastgelelik kaynagindan beslenmez.
            // Desen ADI DEGIL SINIFI tarar - yarin baska bir PRNG adi gelse de yakalar.
            // VAKUM KIRICI: asagidaki POZITIF assertler (gercek veri kaynagi) govdenin
            // okundugunu ve anlamli oldugunu ZATEN kanitliyor.
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali");

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
            // MFIX-3b + KURAL-UYUM DENETIMI: eski vakum kirici ("rngOf HALA kullaniliyor")
            // BU DALGADA GECERSIZ KALDI - rngOf tumden SOKULDU, dolayisiyla ona bakan
            // NotContain artik HICBIR KOSULDA kirilamazdi (bolum 6 vakum yasagi).
            // OLCUT ARTIK KUSUR SINIFI: govde HICBIR rastgelelik kaynagindan beslenmez.
            // Desen ADI DEGIL SINIFI tarar - yarin baska bir PRNG adi gelse de yakalar.
            // VAKUM KIRICI: asagidaki POZITIF assertler (gercek veri kaynagi) govdenin
            // okundugunu ve anlamli oldugunu ZATEN kanitliyor.
            s.Length.Should().BeGreaterThan(200000, "index.html govdesi okunmus olmali");

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
            // MANTIK-FIX-4 / K4: ortusme kontrolu TEK YONLUYDU (yalniz "T'de olup AR'da
            // olmayan"). O halde bir anahtari T'den silip AR'da BIRAKMAK - ya da yalniz
            // AR'a bir anahtar EKLEMEK - pinden SESSIZCE gecerdi ve sozlukte olu yuk
            // birikirdi. Kontrol CIFT YONLU yapildi.
            var eksik = new List<string>();
            foreach (var k in tAnahtar) if (!arAnahtar.Contains(k)) eksik.Add(k);
            eksik.Should().BeEmpty("AR sozlugu T ile TAM ortusmeli (tireli anahtarlar DAHIL)");

            var fazla = new List<string>();
            foreach (var k in arAnahtar) if (!tAnahtar.Contains(k)) fazla.Add(k);
            fazla.Should().BeEmpty(
                "AR sozlugunde T'de KARSILIGI OLMAYAN anahtar kalmamali - tek yonlu kontrol "
                + "T'den silinip AR'da unutulan anahtari GORMEZ");
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
            // KURAL-UYUM DENETIMI: burada `tip` dongu degiskeni KULLANILMIYORDU - ayni iddia uc
            // kez kosuyor, okuyan "uc tipin eslemesi dogrulaniyor" saniyordu ama _TOAST_IKON
            // icinden bir girdi silinse pin YESIL kalirdi. Artik HER TIP AYRI AYRI aranir.
            var ikonBlok = s.Substring(s.IndexOf("var _TOAST_IKON", StringComparison.Ordinal));
            ikonBlok = ikonBlok.Substring(0, ikonBlok.IndexOf('\n'));
            foreach (var tip in new[] { "ok", "err", "info" })
                ikonBlok.Should().Contain(tip + ":", "_TOAST_IKON icinde '" + tip + "' girdisi OLMALI");

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

        // ── P19) MANTIK-FIX-1 / K1 - ISTEMCI FIYATI TEK SINIR NOKTASINDA NORMALIZE EDER ──
        // DURUST ETIKET: bu bir KAYNAK SOZLESMESI pinidir, DAVRANIS pini DEGILDIR (depoda
        // JS/DOM kosucusu yok - Dalga 4'ten beri acik kalem). Davranis kaniti dalganin
        // A/B tarayici olcumlerindedir.
        //
        // OLCULEN GEREKCE: istemcide DOGRUDAN `.price` okumasi 36 nokta (index.html 34 +
        // api-bridge 2), `pPrice(` cagrisi yalnizca 4. Hepsi mapProduct'in URETTIGI nesneyi
        // tuketiyor (detaydanUrun :809 mapProduct cagiriyor, enrichProduct fiyata dokunmuyor,
        // cartSubtotal :1217 ve pPrice :1670 p.price okuyor). Bu yuzden normalizasyon TEK
        // SINIR NOKTASINDA yapilir; 36 tuketici DEGISMEDEN dogru olur ve YENI yazilacak bir
        // yuzey de VARSAYILAN OLARAK dogru olur.
        [Fact]
        public void KAYNAK_SOZLESMESI_IstemciFiyati_SINIRDA_Normalize_Edilir_TEK_NOKTA()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var govde = FonksiyonGovdesi(b, "function mapProduct");
            govde.Should().NotBeNullOrWhiteSpace("mapProduct govdesi okunabilmeli");

            // (1) ASIL IDDIA: fiyat sunucunun ETKIN alanindan turer.
            //
            // L3 DENETCISI PIN BOSLUGU BULDU (AGIR) ve assert ALAN BAZLI hale getirildi:
            // eski hali `Contain("effective_price")` idi ve bu **BEDAVA DOGRU** kaliyordu -
            // dizge govdede DORT satirda geciyor (price, old x2, cart). Denetci `price:`
            // satirini K1 ONCESI haline (`Number(p.price)`) dondurdu ve **TUM SUIT 575/578
            // ile TEMIZ DURUMLA BIREBIR AYNI kaldi** - yani K1'in istemci yarisi PINSIZDI.
            // Olcut artik ALANIN KENDISI: `price` alani etkin fiyattan TUREMELI.
            // (Bosluk ayiklanir ki bicimlendirme degisikligi pini kirmasin - M-P8 dersi.)
            var govdeBosluksuz = govde.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
            govdeBosluksuz.Should().Contain("price:Number(p.effective_price",
                "mapProduct'in `price` ALANI sunucunun etkin fiyat alanindan turemeli - " +
                "govdede dizgenin BASKA satirlarda gecmesi YETMEZ (L3 denetcisi bu boslugu " +
                "uretim mutasyonuyla gosterdi: `price` geri alindi, HICBIR pin yakalamadi)");
            govdeBosluksuz.Should().NotContain("price:Number(p.price)",
                "K1 ONCESI ham fiyat okumasi GERI GELEMEZ");

            // (2) CIFT-ANLAM KIRICI: `old` alani indirimliyken LISTE fiyatini tasimali.
            // Bu olmadan indirim EKRANDA GORUNMEZ: olculdu ki 8 indirimli urunun 7'sinde
            // old_price BOS ve musteri hicbir indirim isareti gormuyor. `old` dolunca
            // mevcut discPct (index.html:2002) ve ustu-cizili makinesi CALISIR.
            govde.Should().Contain("old",
                "eski fiyat alani KORUNMALI - ustu cizili ve yuzde rozeti ona bagli");

            // (3) VAKUM KIRICI: mapProduct HALA diger alanlari da esliyor - yani govde
            // gercekten okundu ve tarama bos bir dizgede kosmuyor.
            govde.Should().Contain("total_stock", "mapProduct hala stok alanini esliyor olmali");
            govde.Should().Contain("average_rating", "mapProduct hala puan alanini esliyor olmali");

            // (4) TEK NOKTA SARTI: fiyat cozumu mapProduct DISINDA bir yerde TEKRARLANMAZ.
            // Bu depoda "ayni kuralin ikinci kopyasi" YEDI kez bedel odetti (B10, D5, K7,
            // Faz 0/K1, D-SEMA, cift tanimli sepetImzasi, cift kupon durumu). Etkin fiyat
            // ikinci bir yerde hesaplanirsa iki taraf ZAMANLA ayrisir.
            var toplamGecis = Regex.Matches(b, "effective_price").Count;
            var govdeGecis = Regex.Matches(govde, "effective_price").Count;
            toplamGecis.Should().Be(govdeGecis,
                "etkin fiyat YALNIZ mapProduct icinde cozulmeli - ikinci bir cozum noktasi " +
                "acilirsa iki taraf zamanla ayrisir (bu depoda yedi kez bedeli odendi)");

            // (5) index.html tarafi DEGISMEZ: pPrice HALA p.price dondurmeli. Fiyat orada
            // yeniden cozulseydi sinir normalizasyonu ANLAMSIZ olurdu.
            var s = YorumlariAyikla(Index);
            s.Should().Contain("function pPrice(p)",
                "pPrice KORUNMALI - sepet ekseninin ortak tabani odur");
            Regex.Matches(s, "effective_price").Count.Should().Be(0,
                "index.html sunucu alan adini BILMEMELI - normalizasyon sinirda yapilir");
        }

        // Bir acilis parantezinin ESLESEN kapanisini bulur (tirnak farkindaligi ile).
        // Toast argumanlari icinde parantez ve tirnak IC ICE gecebiliyor; duz arama
        // yanlis kapanis bulurdu.
        // ── P22) MANTIK-FIX-1 / K3+K4 - KUPON: TEK DURUM ve SEPET-IMZALI TAZELEME ───────
        // DURUST ETIKET: KAYNAK SOZLESMESI pini, DAVRANIS pini DEGIL (JS/DOM kosucusu yok).
        // Davranis kaniti dalganin A/B tarayici olcumlerindedir.
        //
        // OLCULEN ONCE-DURUM (R-M4): sepet 4 -> 1 kuculdu, ekran -159,96 indirimi GOSTERMEYE
        // DEVAM ETTI; dogrusu 31,99 idi (BES KAT). Ayrica IKI BAGIMSIZ KUPON DURUMU vardi:
        // index.html `coupon` (KALICI, dvs_coupon) ile api-bridge `checkoutState.coupon`
        // (BELLEK) ayri yasiyordu - cekmece "kupon yok" derken checkout artik VAR OLMAYAN bir
        // sepetin indirimini dusuyordu.
        [Fact]
        public void KAYNAK_SOZLESMESI_Kupon_TEK_DURUM_ve_SepetImzasiyla_Tazelenir()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // (1) K3: misafir govdesinde SABIT bos kupon kodu GERI GELEMEZ.
            // Olcut LITERAL BICIM DEGIL: bosluk ayiklanmis govdede aranir (M-P8 dersi).
            // KACISSIZ COZUM (kacis-kaybi ailesi - bu depoda BES kez bedeli odendi):
            // regex yerine duz karakter silme. Zincirde ters bolu KAYBOLAMAZ.
            var bosluksuz = b.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
            bosluksuz.Should().NotContain("coupon_code:\"\",",
                "misafir govdesindeki SABIT bos kupon kodu geri gelemez - musteri cekmecede " +
                "indirimi gorup TAM FIYAT oderdi (R-M3'te olculdu)");

            // (2) K4: kupon SEPET IMZASINA bagli olarak SUNUCUYA yeniden sorulur.
            b.Should().Contain("kuponuTazele",
                "sepet degisince kupon sunucuya YENIDEN SORULMALI - yuzde kuponda indirim " +
                "sepetle ORANTILIDIR ve bayat kalirsa musteri yanlis toplam gorur");
            var tazeleGovde = FonksiyonGovdesi(b, "async function kuponuTazele");
            tazeleGovde.Should().NotBeNullOrWhiteSpace("kuponuTazele govdesi okunabilmeli");
            tazeleGovde.Should().Contain("sepetImzasi",
                "tazeleme SEPET IMZASINA baglanmali - salt-cizim yollari (dil, para birimi, " +
                "sekme) istek URETMEMELI; MFIX-3b/T1'de bu tuzagin bedeli odendi");
            // PREMIS GUNCELLEMESI (celiski avcisi bulgusu, gerekce yazili): assert once
            // `divisimaValidateCoupon` ariyordu. O sarmalayici HER hatayi null'a ceviriyordu -
            // sunucunun "gecersiz" (4xx) yaniti ile "sunucuya ULASILAMADI" (ag/5xx) AYIRT
            // EDILEMIYORDU ve gecici bir kesintide GECERLI kupon dusuruluyordu. Tazeleme artik
            // ayrimi YAPAN `divisimaKuponDurumu`yu kullaniyor. OLCULEN SEY DEGISMEDI
            // ("tazeleme SUNUCUYA sorar"), yalnizca cagrilan fonksiyonun adi degisti; assert
            // ayrica AYRIMIN KENDISINI de tutar hale getirildi - yani pin GUCLENDI.
            tazeleGovde.Should().Contain("divisimaKuponDurumu",
                "tazeleme SUNUCUYA sormali - yerel hesapla yetinmek bayatligi cozmez");
            tazeleGovde.Should().Contain("ulasildi",
                "sunucuya ULASILAMADIGINDA kupon DUSURULMEMELI - 'gecersiz' ile 'soramadim' " +
                "ayirt edilmezse gecici bir kesinti GECERLI bir kuponu kaldirir ve sebebi " +
                "YANLIS soylenir (gorunur ama yanlis mesaj)");

            // (3) TEK DURUM: checkoutState.coupon BAGIMSIZ YASAYAMAZ.
            b.Should().Contain("kuponDurumunuEsitle",
                "checkout kupon durumu cekmecenin kupon durumundan TURETILMELI - iki bagimsiz " +
                "durum UC ayri ayrisma uretiyordu (A3/2D-2E ve [ANA][16])");

            // (4) VAKUM KIRICI: kupon makinesi HALA YERINDE - tarama bos dizgede kosmuyor.
            b.Should().Contain("divisimaSetCheckoutCoupon", "kupon koprusu HALA var olmali");
            b.Should().Contain("checkoutState.coupon", "checkout kupon alani HALA kullaniliyor olmali");

            // (5) CIFT-ANLAM KIRICI: SESSIZ DUSURME YASAK.
            // MFIX-B/K2 sunucuda "gecersiz kupon sessizce yok sayilmaz" sozlesmesini kurmustu;
            // istemcide sessizce kaldirmak ayni kusurun ikizi olurdu. Tazeleme kuponu
            // dusuruyorsa kullaniciya GORUNUR ve CEVIRILI bir mesaj vermeli.
            tazeleGovde.Should().Contain("ceviri(",
                "kupon dusurulurken GORUNUR ve CEVIRILI mesaj verilmeli - sessiz dusurme " +
                "MFIX-B/K2'nin sunucuda kapattigi kusurun istemci ikizidir");

            // (6) index.html'in kupon nesnesi min degerini SUNUCUDAN almali.
            // A3/2A: `min:0` SABITTI, bu yuzden validateCoupon (index.html:2584) guard'i
            // `cartRaw() < 0` sartina bagliydi ve HICBIR KOSULDA atesleyemiyordu.
            var s = YorumlariAyikla(Index);
            s.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "")
             .Should().NotContain("min:0,srvAmount",
                "kupon nesnesindeki SABIT min:0 geri gelemez - guard'i yapisal olarak olu kilar");
        }

        // ── P23) MANTIK-FIX-1 / K5+K6 - KARSILIGI OLMAYAN VAAT ve ESIK METNI ───────────
        // DURUST ETIKET: KAYNAK SOZLESMESI pini. Davranis kaniti R-M5/R-M6 A/B olcumleridir.
        //
        // K5 OLCULEN ONCE-DURUM (R-M5): bulten penceresi "%10 indirim kodu" VAAT EDIYORDU ve
        // veritabaninda karsiligi YOKTU. MFIX-1 satir ici metinlerden YALNIZ BIRINE dokunmustu
        // (nl_done_s) ve applyI18n (index.html:2909) `el.textContent = t(...)` ile onu da
        // SOZLUKTEN GERI YAZIYORDU - yani duzeltme YAPILMIS GORUNUP CALISMIYORDU. Vaat DORT
        // noktadaydi: nl_title · nl_sub · nl_btn · nl_done_s, UC DILDE.
        //
        // K6 OLCULEN ONCE-DURUM (R-M6): kargo esigi metinleri `>=` davranisiyla CELISIYORDU.
        // Kod HER YERDE `>=` (OrderManager :293, misafir :1534, uye :1695, cubuk :2606), yani
        // TAM 2.000,00'de kargo BEDAVA; ama TR `ben_ship_s` "2.000 TL UZERI" ve EN/AR karsiliklari
        // "over"/"فوق" diyordu - metin `>` IMA EDIYORDU. K6 SAF METIN ISIDIR, davranis DEGISMEZ.
        [Fact]
        public void KAYNAK_SOZLESMESI_KarsiligiOlmayanVaat_YOK_ve_EsikMetni_Davranisla_Tutarli()
        {
            var s = Oku("frontend/index.html");   // YORUM AYIKLAMA YOK: sozluk ve HTML metni aranir

            // ── K5: "%10" VAADI DORT ANAHTARIN UCUNDE DE, UC DILDE DE KALMAMALI ──
            // [YOKLUK] iddiasi. Anahtar bazinda taranir; boylece "bir dilde unutuldu" durumu
            // GORUNUR olur (R-M5'te tam bu olmustu - MFIX-1 dortten birine dokunmustu).
            foreach (var anahtar in new[] { "nl_title", "nl_sub", "nl_btn", "nl_done_s" })
            {
                foreach (var m in Regex.Matches(s, "[,{]" + anahtar + ":[^,}]{0,240}").Cast<Match>())
                {
                    m.Value.Should().NotContain("10%",
                        anahtar + " karsiligi olmayan bir indirim kodu VAAT ETMEMELI (EN/AR)");
                    m.Value.Should().NotContain("%10",
                        anahtar + " karsiligi olmayan bir indirim kodu VAAT ETMEMELI (TR)");
                }
            }
            // SATIR ICI HTML varsayilanlari da temiz olmali - applyI18n sozlukten geri yazsa
            // bile, JS calismadan once kullanicinin gordugu METIN BUDUR.
            s.Should().NotContain("%10 İndirim Seni Bekliyor",
                "satir ici HTML varsayilani da vaadi tasimamali");

            // KAPSAM GENISLETMESI - IKI DENETCI BAGIMSIZ OLARAK AYNI BOSLUGU BULDU:
            // K5 sozluk METINLERINI temizledi ama modalin EN BASKIN ogesi olan dekoratif
            // rozet (`nl-deco`, 58px / mobilde 46px) HALA "%10" yaziyordu. Modal ilk ziyarette
            // ~5 sn sonra KENDILIGINDEN aciliyor. L3 denetcisi rozeti "%10 INDIRIM" yapip
            // vaadi BUYUTTU ve P23 YESIL KALDI - yani anahtar bazli tarama bu yuzeyi
            // KAPSAMIYORDU. Rozet artik ADIYLA taraniyor.
            var deco = Regex.Matches(s, @"class=""nl-deco""[^<]{0,40}>[^<]{0,60}<").Cast<Match>().FirstOrDefault();
            deco.Should().NotBeNull("bulten rozeti (nl-deco) HALA var olmali - vakum kirici");
            deco!.Value.Should().NotContain("%",
                "bulten rozeti YUZDE bir indirim VAAT ETMEMELI - metinler temizlense bile " +
                "modalin en baskin ogesi vaat ediyorsa kusur KAPANMAMISTIR");

            // VAKUM KIRICI: bulten penceresi HALA VAR ve anahtarlari HALA tanimli.
            // "hepsini sil" YANLIS duzeltmedir - pencere mesru bir e-bulten kaydidir.
            Regex.Matches(s, "[,{]nl_title:").Count.Should().BeGreaterThan(1,
                "nl_title T ve AR'da HALA tanimli olmali (pencere SILINMEDI)");
            s.Should().Contain("nlModal", "bulten penceresi HALA var olmali");

            // ── K6: ESIK METNI `>=` DAVRANISIYLA TUTARLI ──
            // Kod tarafi DEGISMEZ; degisen yalnizca METIN. Bu yuzden once davranisin `>=`
            // oldugunu DOGRULARIZ (premis pini) - kod `>` olsaydi metin duzeltmesi YANLIS olurdu.
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            b.Should().Contain(">= 2000",
                "kargo esigi karsilastirmasi `>=` olmali - metin duzeltmesinin PREMISI budur");

            // TR: "uzeri" tek basina `>` ima eder; "ve uzeri" `>=` demektir.
            // KACISSIZ: verbatim dizge - ters bolu zincirde kaybolamaz (aile dersi, 6 ornek).
            var trEsik = Regex.Matches(s, @"[,{]ben_ship_s:\[[^]]{0,120}\]").Cast<Match>().FirstOrDefault();
            trEsik.Should().NotBeNull("ben_ship_s T'de tanimli olmali");
            trEsik!.Value.Should().Contain("ve üzeri",
                "TR metni `>=` demeli - kod TAM 2.000,00'de kargoyu BEDAVA yapiyor");
            trEsik.Value.Should().NotContain("over",
                "EN karsiligi `over` DEMEMELI - `over 2,000` 2.000,00'i DISLAR");

            // AR karsiligi da ayni semantigi tasimali (uc dilde AYNI vaat).
            var arEsik = Regex.Matches(s, "[,{]ben_ship_s:'[^']{0,120}'").Cast<Match>().FirstOrDefault();
            arEsik.Should().NotBeNull("ben_ship_s AR'da tanimli olmali");
            arEsik!.Value.Should().NotContain("فوق",
                "AR metni `fevk` (uzeri/ustunde) DEMEMELI - kod esigi DAHIL ediyor");

            // CIFT-ANLAM KIRICI: ZATEN DOGRU olan anahtar BOZULMAMALI.
            // ann_free_ship "{tutar} ve uzeri" diyor ve esigi FREE_SHIP'ten parametrik aliyor
            // (index.html:2884). K6 onu DEGISTIRMEMELI - yoksa esik iki yerde ayrisir.
            s.Should().Contain("{tutar}",
                "duyuru seridi esigi PARAMETRIK almaya DEVAM etmeli (FREE_SHIP tek kaynak)");
        }

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

        // ── P-F3) MANTIK-FIX-2R / K3 - FATURA GOVDESI ISTEMCIDE, UC DILDE ─────────────
        //
        // DURUST ETIKET: bu bir KAYNAK SOZLESMESI pinidir - depoda JS/DOM kosucusu YOK
        // (Dalga 4'ten beri acik kalem). Davranis kaniti R-F3a/b/c tarayici olcumleridir
        // (uc dilde ekran, sizinti dedektoru EN 0 / AR 0, iptal isareti, bos durum).
        //
        // OLCULEN ONCE-DURUM: fatura govdesi %100 SUNUCU HTML'iydi (17 TR dizge, lang="tr",
        // sabit " TL", dd.MM.yyyy) ve istemci onu guvenliYaz -> DOMPurify ile basiyordu.
        // dvsLocale bir FRONTEND fonksiyonu oldugu icin o dizeye ERISEMIYORDU.
        [Fact]
        public void KAYNAK_SOZLESMESI_FaturaGovdesi_ISTEMCIDE_Kurulur_UC_DILDE_ve_DB_METNI_textContent()
        {
            var bridge = Oku("frontend/api-bridge.js");
            var index = Oku("frontend/index.html");

            // (1) ESKI HTML-ENJEKSIYON YOLU OLDU - olu kod kalmadi.
            bridge.Should().NotContain("guvenliYaz(kutu, html",
                "fatura govdesi artik sunucu HTML'i DEGIL - eski enjeksiyon yolu KALMAMALI");
            // VAKUM KIRICI: guvenliYaz'in KENDISI hala var (sozlesme sayfalari onu kullaniyor) -
            // "fonksiyonu tumden sil" YANLIS duzeltmedir.
            bridge.Should().Contain("function guvenliYaz(",
                "guvenliYaz baska ekranlarda KULLANILIYOR - kaldirilmamali");

            // (2) GOVDE ISTEMCIDE KURULUR ve K2 alanlarindan beslenir.
            var ciz = FonksiyonGovdesi(bridge, "function faturaGovdesiniCiz(");
            ciz.Should().NotBeNullOrWhiteSpace("renderer TANIMLI olmali");

            // B3 (MK-4b denetim bulgusu): TANIM YETMEZ, CAGRI DA OLCULUR.
            // Mutasyonla gosterildi: cagri yeri guvenliYaz(kutu, d.html, ...) ile degistirilince
            // K3'un TAMAMI bypass oluyor, renderer OLU KODA doniyor ve pin YESIL kaliyordu.
            // Depoda MFIX-3b/M4 ile AYNI bosluk sinifi ("tanim + cagri = en az iki gecis").
            // KAPSAM DAR: guvenliYaz(kutu, ...) sozlesme sayfalarinda MESRU olarak kullaniliyor
            // (api-bridge.js), bu yuzden yasak DOSYA GENELINE degil FATURA MODALINA konur.
            var modal = FonksiyonGovdesi(bridge, "function faturaModalAc(");
            modal.Should().NotBeNullOrWhiteSpace("fatura modali TANIMLI olmali");
            modal.Should().Contain("faturaGovdesiniCiz(",
                "fatura modali govdeyi RENDERER ile kurmali - tanimli ama cagrilmayan renderer OLU KODDUR");
            modal.Should().NotContain("guvenliYaz(",
                "fatura kutusu HTML enjeksiyonuyla DOLDURULAMAZ - eski yol farkli argumanla GERI GELEMEZ");
            foreach (var alan in new[] { "has_invoice", "is_shipping", "vat_breakdown", "payment",
                                          "invoice_is_cancelled", "order_is_cancelled" })
                ciz.Should().Contain(alan, $"K2 sozlesme alani '{alan}' renderer'da kullanilmali");

            // (3) DB'DEN GELEN METIN textContent ILE - escape'siz innerHTML'e DB verisi GIRMEZ.
            ciz.Should().Contain("textContent", "DB metinleri textContent ile yazilmali");
            // B2 (MK-4b denetim bulgusu): olcut BOSLUKTAN BAGIMSIZ olmali. Onceki hal
            // "innerHTML = " literalini ariyordu ve "ad.innerHTML=..." bicimindeki AYNI
            // regresyon pinden GECIYORDU (mutasyonla gosterildi). Bu, depoda bedeli odenmis
            // "literal bicim degil KUSUR SINIFI olculur" dersinin frontend karsiligidir.
            var cizSik = Regex.Replace(ciz, @"\s+", "");
            cizSik.Should().NotContain("innerHTML=", "renderer innerHTML ile DB verisi BASMAMALI");

            // (4) KARGO ETIKETI SOZLUKTEN (E4) - DB'deki ad ekrana BASILAMAZ.
            // Sunucu product_name'i NULL gonderiyor; renderer is_shipping dalinda ceviri() kullanir.
            ciz.Should().Contain("is_shipping ? ceviri(\"b_kargo\")",
                "kargo etiketi SOZLESMEDEN + SOZLUKTEN cizilmeli, DB adindan DEGIL");

            // (5) PARA/TARIH dvsLocale uzerinden (paraTL / tarihBicimi) - sunucu bicimlemiyor.
            ciz.Should().Contain("paraTL(", "para bicimleme istemcide dvsLocale ile olmali");
            ciz.Should().Contain("tarihBicimi(", "tarih bicimleme istemcide dvsLocale ile olmali");

            // (6) YENI ANAHTARLAR UC DILDE ve MUKERRER DEGIL.
            // Ankrajli sayim (ship_s dersi: "X:" deseni "onek_X:" ICINDE de esler).
            var yeniAnahtarlar = new[] { "b_siparis", "b_fatura_no", "b_tarih", "b_birim_fiyat",
                "b_tutar", "b_matrah", "b_kdv", "b_genel_toplam", "b_kdv_kirilimi",
                "b_odeme_ozeti", "b_fatura_bu_siparise_yok", "b_fatura_iptal_edildi" };
            foreach (var k in yeniAnahtarlar)
            {
                var n = AnkrajliSayim(index, k);
                n.Should().Be(2, $"'{k}' T ve AR sozluklerinde BIRER kez tanimli olmali " +
                                  "(2'den fazlasi MUKERRER anahtar demektir - son tanim digerini sessizce EZER)");
            }

            // VAKUM KIRICI: sayim yontemi GERCEKTEN calisiyor olmali.
            AnkrajliSayim(index, "b_kargo").Should().Be(2, "mevcut bir anahtar da 2 saymali");
            AnkrajliSayim(index, "b_zzz_olmayan_anahtar").Should().Be(0, "olmayan anahtar 0 saymali");

            // CIFT-ANLAM KIRICI: mevcut b_fatura_yok'a DOKUNULMADI - o "hic faturan yok"
            // (liste) anlamindadir; belge-yok icin AYRI anahtar acildi. Ayni anahtari iki
            // anlamda kullanmak bu depoda bedeli odenmis bir siniftir.
            AnkrajliSayim(index, "b_fatura_yok").Should().Be(2, "mevcut liste-bos anahtari KORUNMALI");
            index.Should().Contain("b_fatura_yok:['Henüz faturan yok.'",
                "mevcut anahtarin DEGERI degismemeli");
        }


        // ── P-H3c) MANTIK-FIX-3 / K3 - ISTEMCI SOZLESMESI ────────────────────────────
        //
        // DURUST ETIKET: KAYNAK SOZLESMESI pini (depoda JS/DOM kosucusu YOK). Davranis
        // kaniti R-H3 tarayici olcumleridir (uc dilde yanlis-sifre metni, basarili
        // degisim, eski 401 / yeni 200).
        //
        // OLCULEN ONCE-DURUM: #pfPassSave api-bridge'de SIFIR gecis; index.html'in
        // savePassForm govdesi HICBIR API CAGRISI YAPMADAN "Sifren guncellendi" diyordu
        // (MFIX-1'de sokulen coFinish ile AYNI SINIF) ve yerel "en az 6 karakter" kurali
        // tasiyordu - sunucu 8 + karmasiklik istiyor.
        [Fact]
        public void KAYNAK_SOZLESMESI_SifreDegistirme_Baglidir_YalanMock_Sokuldu_PolitikaKopyasi_Yok()
        {
            var bridge = Oku("frontend/api-bridge.js");
            var index = Oku("frontend/index.html");

            // (1) GERCEK BAGLAMA: buton VAR ve sunucu ucunu CAGIRIYOR.
            bridge.Should().Contain("pfPassSave", "sifre butonu api-bridge'de baglanmali");
            bridge.Should().Contain("api.account.changePassword(",
                "handler GERCEK ucu cagirmali - tanimli ama cagrilmayan sarmalayici OLU KODDUR");

            // (2) YALAN MOCK SOKULDU: govde artik API'ye gitmeden BASARI DEMEZ.
            var mock = FonksiyonGovdesi(index, "function savePassForm(");
            mock.Should().NotBeNullOrWhiteSpace("mock govdesi okunabilmeli");
            var mockSik = Regex.Replace(mock, @"\s+", "");
            mockSik.Should().NotContain("pf_pass_ok",
                "API'ye gitmeden 'guncellendi' diyen yol GERI GELEMEZ");
            mockSik.Should().NotContain("nw.length<6",
                "yerel ve YANLIS uzunluk kurali GERI GELEMEZ (sunucu 8 + karmasiklik istiyor)");

            // (3) ISTEMCIDE SIFRE POLITIKASININ IKINCI KOPYASI ACILMADI (merkez N2).
            // Handler govdesinde uzunluk/karmasiklik karari ARANMAZ; yalniz form-duzeyi
            // iki kontrol mesrudur ("iki alan dolu mu", "iki yeni sifre esit mi") -
            // ikincisini sunucu ZATEN GOREMEZ, dogrulama alani ona hic gitmez.
            var handler = FonksiyonGovdesi(bridge, "if (pps) pps.onclick = function ()");
            handler.Should().NotBeNullOrWhiteSpace("handler govdesi okunabilmeli");
            var hSik = Regex.Replace(handler, @"\s+", "");
            // MK-4b DENETIM BULGUSU (ITIRAZ-1) - OLCUT BICIMDEN BAGIMSIZ HALE GETIRILDI.
            // Eski hali YALNIZ ".length<" LITERAL BICIMINI ariyordu; denetci handler govdesine
            // sunucunun kuralinin BIREBIR REGEX KOPYASINI ekledi ve pin **27/27 YESIL** kaldi.
            // Bu, MFIX-2/M-P8 ("assert ESKI LITERAL BICIMI ariyordu, KUSUR SINIFINI degil") ve
            // MANTIK-FIX-2R/B2 ("innerHTML = " bosluksuz bicimi kaciriyordu) ile AYNI SINIF.
            // Artik KUSUR SINIFI taraniyor: uzunluk karari HANGI BICIMDE yazilirsa yazilsin
            // ve karmasiklik kararinin klasik yazimi (regex ileri-bakis) da yakalanir.
            foreach (var politikaIzi in new[] { ".length<", ".length>", ".length!=", ".length==", "(?=" })
                hSik.Should().NotContain(politikaIzi,
                    $"istemci uzunluk/karmasiklik kurali KOYMAMALI - politika SUNUCUNUN ({politikaIzi})");
            hSik.Should().Contain("pf_pass_match", "form-duzeyi eslesme kontrolu KALMALI");

            // (4) YENI ANAHTAR UC DILDE ve MUKERRER DEGIL (b_fatura_yok dersi).
            AnkrajliSayim(index, "b_mevcut_sifre_hatali").Should().Be(2,
                "yeni anahtar T ve AR sozluklerinde BIRER kez tanimli olmali");
            // VAKUM KIRICI: sayim yontemi calisiyor olmali.
            AnkrajliSayim(index, "b_sifre_kurali").Should().Be(2, "mevcut anahtar da 2 saymali");
            AnkrajliSayim(index, "b_zzz_olmayan").Should().Be(0, "olmayan anahtar 0 saymali");

            // (5) HATALI KURAL ANLATAN ESKI ANAHTAR KULLANILMIYOR: pf_pass_short "6 karakter"
            // diyor ve sunucu 8 istiyor.
            // MK-4b DENETIM DUZELTMESI (ITIRAZ-2): eski yorum "anahtar sozlukte DURUYOR ama BU
            // AKISTA kullanilmaz" diyordu - EKSIK IFADE. Olculdu: `pf_pass_short` (ve mock
            // sokumuyle olu kalan `pf_err` / `pf_pass_ok`) HICBIR akista kullanilmiyor; uc
            // dosyada da cagiran sayisi 0, gecisleri YALNIZ T ve AR sozluk tanimlari.
            // MANTIK-FIX-4 / K4: uc olu anahtar (pf_err / pf_pass_ok / pf_pass_short) T ve
            // AR sozluklerinden KALDIRILDI - MF-3'te "dokunulmaz" olmalarinin sebebi o
            // dalganin sinirlariydi, kusurun kendisi degil. Bu satir artik yalnizca
            // "hatali kural anlatan anahtar bu akista kullanilmaz" degil, "sozlukte de
            // YOK" anlamina gelir; ikinci iddia asagida ayrica assert ediliyor.
            hSik.Should().NotContain("pf_pass_short",
                "6 karakter diyen anahtar sifre degistirme akisinda KULLANILMAMALI");
            foreach (var olu in new[] { "pf_err", "pf_pass_ok", "pf_pass_short" })
                AnkrajliSayim(index, olu).Should().Be(0,
                    $"'{olu}' olu anahtari sozlukten kaldirilmis olmali (cagirani 0'di)");
        }
        // ── P-H6) MANTIK-FIX-3 / K3b - PROFIL KAYDETME SUNUCUYA GIDER ────────────────
        //
        // DURUST ETIKET: KAYNAK SOZLESMESI pini (depoda JS/DOM kosucusu YOK). Davranis
        // kaniti R-H6 tarayici + DB olcumleridir: ONCE toast "Bilgilerin guncellendi"
        // derken /api/ istegi 0 ve musteri satiri DEGISMEDI; SONRA /api/account/profile
        // istegi 1, ad DEGISTI, phone/dogum KORUNDU, e-posta DEGISMEDI.
        //
        // OLCULEN ONCE-DURUM: #pfSave index.html'in saveProfileForm mock'una BAGLIYDI ve
        // o govde HICBIR API CAGRISI YAPMADAN "Bilgilerin guncellendi" diyordu (K3'te
        // sokulen savePassForm ve MFIX-1'de sokulen coFinish ile AYNI SINIF).
        [Fact]
        public void KAYNAK_SOZLESMESI_ProfilKaydetme_SunucuyaGider_YalanMock_Sokuldu_VeriKaybi_Yok()
        {
            var bridge = Oku("frontend/api-bridge.js");
            var index = Oku("frontend/index.html");

            // (1) GERCEK BAGLAMA: buton VAR ve iki ucu da CAGIRIYOR (yukleme + kaydetme).
            bridge.Should().Contain("pfSave", "profil butonu api-bridge'de baglanmali");
            var blok = FonksiyonGovdesi(bridge, "var ps = document.getElementById(\"pfSave\");");
            blok.Should().NotBeNullOrWhiteSpace("baglama blogu okunabilmeli");
            var bSik = Regex.Replace(blok, @"\s+", "");
            bSik.Should().Contain("api.account.updateProfile(",
                "handler GERCEK ucu cagirmali - tanimli ama cagrilmayan sarmalayici OLU KODDUR");
            bSik.Should().Contain("api.account.summary(",
                "form SUNUCUDAN yuklenmeli - yerel depodan beslenen form GERCEK veri gostermez");

            // (2) YALAN MOCK SOKULDU: govde artik API'ye gitmeden BASARI DEMEZ.
            var mock = FonksiyonGovdesi(index, "function saveProfileForm(");
            mock.Should().NotBeNullOrWhiteSpace("mock govdesi okunabilmeli");
            var mSik = Regex.Replace(mock, @"\s+", "");
            mSik.Should().NotContain("pf_saved",
                "API'ye gitmeden 'guncellendi' diyen yol GERI GELEMEZ");
            mSik.Should().NotContain("lsSet('dvs_user'",
                "yerel depoya yazip 'kaydettim' diyen yol GERI GELEMEZ");
            mSik.Should().Contain("b_profil_guncellenemedi",
                "IKINCI SAVUNMA HATTI: api-bridge yuklenmese bile ekran DURUST hata vermeli");

            // (3) VERI KAYBI GUARD'I (PUT-ez semantigi / devir listesindeki F5):
            // DTO uc alan tasir; yalniz {name} gonderilirse phone ve birthdate NULL yazilir.
            bSik.Should().Contain("phone:", "phone GONDERILMELI - yoksa sunucu onu NULL yazar");
            bSik.Should().Contain("birthdate:", "birthdate GONDERILMELI - yoksa sunucu onu NULL yazar");

            // (4) E-POSTA KORKULUGU: alan readonly ve govdede HIC gonderilmiyor.
            index.Should().Contain("id=\"pfEmail\" type=\"email\" value=\"'+esc(userEmail||'')+'\" readonly",
                "e-posta alani markup DUZEYINDE readonly olmali (api-bridge yuklenmese de)");
            bSik.Should().NotContain("email:",
                "e-posta bu dalgada DEGISTIRILMEZ - govdeye KONULAMAZ (DTO da tasimiyor)");

            // (5) ISTEMCIDE DOGRULAMA KOPYASI ACILMADI (merkez N2): "ad bos" KARARI
            // SUNUCUNUNDUR.
            // MANTIK-FIX-4 / K5 - BILINCLI PREMIS DEGISIKLIGI: esleme `wireAccount`
            // kapsamindan IIFE UST DUZEYINE tasindi (misafir checkout'un da erisebilmesi
            // icin), dolayisiyla `v_name` artik BU GOVDEDE degil MERKEZDEKI capa
            // tablosunda yasiyor. OLCULEN SOZLESME AYNI - "istemci on-dogrulama yapmaz" -
            // yalniz olcum YERI degisti: eskiden "cagridan SONRA gecmeli", simdi "bu
            // govdede HIC gecmemeli, merkezde GECMELI".
            bSik.IndexOf("api.account.updateProfile(", StringComparison.Ordinal)
                .Should().BeGreaterThan(-1, "cagri bulunmali");
            bSik.Should().NotContain("v_name",
                "ad kontrolu istemcide ON-DOGRULAMA olarak yapilmamali - karar SUNUCUNUN");
            Bosluksuz(YorumlariAyikla(Oku("frontend/api-bridge.js")))
                .Should().Contain(Bosluksuz("\"ad bos olamaz\", \"v_name\""),
                    "v_name esleme MERKEZINDEKI capa tablosunda tanimli olmali");

            // (6) CIFT-ANLAM KIRICI: "her hataya notr mesaj" uygulamasi bu pini GECEMEZ -
            // notr dal da, merkeze bagli esleme de AYRI AYRI bulunmali.
            bSik.Should().Contain("b_profil_guncellenemedi", "notr hata dali bulunmali");
            bSik.Should().Contain("hataAnahtari(e)",
                "esleme TEK MERKEZDEN gelmeli - satir ici kopya ACILMAMALI");

            // (7) VAKUM KIRICILAR: sokum "fonksiyonu sil" DEGIL - govde YERINDE durmali,
            // ve okunan bloklar bos olmamali.
            index.Should().Contain("function saveProfileForm(", "mock fonksiyonu SILINMEDI, govdesi DURUSTLESTI");
            mSik.Length.Should().BeGreaterThan(10, "mock govdesi bos okunmus olamaz");
            bSik.Length.Should().BeGreaterThan(200, "baglama blogu bos okunmus olamaz");

            // (8) YENI ANAHTARLAR UC DILDE ve MUKERRER DEGIL (b_fatura_yok dersi).
            AnkrajliSayim(index, "b_profil_guncellenemedi").Should().Be(2,
                "yeni anahtar T ve AR sozluklerinde BIRER kez tanimli olmali");
            AnkrajliSayim(index, "b_eposta_degistirilemez").Should().Be(2,
                "yeni anahtar T ve AR sozluklerinde BIRER kez tanimli olmali");
            // Yeniden KULLANILAN mevcut anahtarlar da uc dilde tanimli olmali.
            AnkrajliSayim(index, "pf_saved").Should().Be(2, "basari mesaji MEVCUT anahtardan gelir");
            AnkrajliSayim(index, "v_name").Should().Be(2, "ad-bos mesaji MEVCUT anahtardan gelir");
            AnkrajliSayim(index, "b_zzz_olmayan_k3b").Should().Be(0, "olmayan anahtar 0 saymali");
        }

        // Ankrajli anahtar sayimi: "X:" deseni "onek_X:" ICINDE de eslesir (ship_s dersi).
        private static int AnkrajliSayim(string kaynak, string anahtar)
        {
            int n = 0;
            for (int i = 1; i < kaynak.Length; i++)
            {
                if (kaynak[i] != anahtar[0]) continue;
                if (i + anahtar.Length + 1 > kaynak.Length) break;
                if (string.CompareOrdinal(kaynak, i, anahtar, 0, anahtar.Length) != 0) continue;
                if (kaynak[i + anahtar.Length] != ':') continue;
                var onceki = kaynak[i - 1];
                if (onceki == ',' || onceki == '{' || onceki == ' ') n++;
            }
            return n;
        }

        // Bosluk-ayiklanmis karsilastirma. KACISSIZ - `\s` regex'i bu depoda yazim
        // zincirinde IKI KEZ kayboldu (kacis-kaybi ailesi), bu yuzden karakter suzgeci.
        private static string Bosluksuz(string s) =>
            string.Concat(s.Where(c => !char.IsWhiteSpace(c)));

        // ── P-V1 (MANTIK-FIX-4 / K1): "INDIRIM" SUZGECI GERCEK INDIRIMI GOSTERIR ─────────
        // Suzgecin TEK olcutu `p.old` (index.html 2049 cip · 2054 kategori · 2118 serit ·
        // 2175 sidebar sayaci). Bu pin o alanin KAPISINI tutar: `old` yalnizca ETKIN FIYAT
        // LISTE FIYATINDAN KUCUKKEN dolar; `old_price` tek basina kumeye SOKMAZ, yalnizca
        // DEGERI secer.
        // OLCUT BICIMDEN BAGIMSIZ: kapinin once geldigi INDEKS KARSILASTIRMASIYLA olculur -
        // bosluk, satir sonu ya da parantez duzeni degisse de ayni kusuru yakalar.
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir, davranis pini DEGILDIR (depoda JS/DOM
        // kosucusu YOK - Dalga 4'ten beri acik kalem). Davranis kaniti MANTIK-FIX-4'un
        // once/sonra olcumudur: suzgec sayaci 9 -> 8, urun 1'in rozeti ve ustu cizili
        // fiyati gitti, kalan 8 urunun old/pct degerleri BIREBIR AYNI kaldi.
        [Fact]
        public void KAYNAK_SOZLESMESI_IndirimSuzgeci_GERCEK_INDIRIM_Kumesini_Gosterir()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var govde = FonksiyonGovdesi(b, "function mapProduct(p)");

            // VAKUM KIRICI 1: govde gercekten okunmus olmali.
            govde.Length.Should().BeGreaterThan(200,
                "mapProduct govdesi okunmus olmali - bos govde her iddiayi BEDAVA dogru yapardi");

            var bas = govde.IndexOf("old:", StringComparison.Ordinal);
            bas.Should().BeGreaterThan(-1, "mapProduct hala `old` alanini uretmeli");
            var kalan = govde.Substring(bas);

            var iKapi = kalan.IndexOf("effective_price", StringComparison.Ordinal);
            var iDeger = kalan.IndexOf("old_price", StringComparison.Ordinal);

            iKapi.Should().BeGreaterThan(-1,
                "`old` kapisi ETKIN FIYATA bakmali - suzgec kumesi gercek indirimden turemeli");

            // VAKUM KIRICI 2: old_price TUMDEN silinmis olamaz. Silinseydi kapi iddiasi
            // bedava dogru olurdu, ama uc fiyatli urun (123: 299,90/249,90/399,90) ustu
            // cizili fiyatini KAYBEDERDI.
            iDeger.Should().BeGreaterThan(-1,
                "old_price DEGER kaynagi olarak okunmaya devam etmeli");

            iKapi.Should().BeLessThan(iDeger,
                "KAPI DEGERDEN ONCE gelmeli: once 'gercekten indirim var mi' sorulur, SONRA "
                + "hangi degerin ustu cizilecegi secilir. Ters sirada old_price TEK BASINA "
                + "kumeye sokar ve indirimi olmayan urun 'Indirim'de rozetle listelenir.");

            // CIFT-ANLAM KIRICI: `price` normalizasyonu (MF-1/K1) DEGISMEMELI - K1 yalniz
            // `old` kapisini daraltir, etkin fiyati DEGISTIRMEZ.
            Bosluksuz(govde).Should().Contain(Bosluksuz("price: Number(p.effective_price ?? p.price) || 0"),
                "etkin fiyat normalizasyonu MANTIK-FIX-1'deki haliyle durmali");

            // SINIR: normalizasyon api-bridge'de KALIR. api-client.js iki istemcinin
            // PAYLASTIGI dosyadir ve admin formu `price`i TAM-VARLIK Update'e geri yazar;
            // oraya tasinirsa indirim her kayitta bir kademe daha duser (MFIX-B kalibi).
            Oku("frontend/api-client.js").Should().NotContain("effective_price",
                "fiyat normalizasyonu admin panelinin de okudugu dosyaya TASINMAMALI");
        }

        // ── P-V2 (MANTIK-FIX-4 / K2): SIPARIS KARTI TUTARIN NE OLDUGUNU SOYLER ──────────
        // Kart `o.total` = orders.total_price basar; bu SIPARISIN BRUT toplamidir, magaza
        // kredisi DUSULMEMISTIR (olculdu: 261 -> total 689,74 / kredi 200,00 / kasadan
        // odenen 489,74). Etiketsiz rakam "odedigim tutar" diye okunabiliyordu.
        // ETIKET KOSULSUZ: liste DTO'su krediyi TASIMIYOR, dolayisiyla kart "kredili mi"
        // sorusunu YANITLAYAMAZ; kosullu bir etiket ya olu dal yazar ya da sunucudan yeni
        // alan ister. Kosulsuz etiket iki durumda da DOGRUDUR.
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti muhurdeki uc dilli
        // once/sonra olcumudur (261 ve kredisiz 260 AYNI etiketi tasiyor).
        [Fact]
        public void KAYNAK_SOZLESMESI_SiparisKarti_TutarEtiketini_KOSULSUZ_Tasir()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));
            var govde = FonksiyonGovdesi(b, "async function sekmeSiparisler(el)");

            govde.Length.Should().BeGreaterThan(200, "sekmeSiparisler govdesi okunmus olmali");

            var iMeta = govde.IndexOf("ao-meta", StringComparison.Ordinal);
            iMeta.Should().BeGreaterThan(-1, "kart tutar bloku hala ao-meta ile cizilmeli");
            var iTutar = govde.IndexOf("paraTL(o.total)", StringComparison.Ordinal);

            // VAKUM KIRICI: tutarin kendisi HALA basiliyor olmali.
            iTutar.Should().BeGreaterThan(iMeta, "tutar ao-meta blokunda basilmaya devam etmeli");

            var arasi = govde.Substring(iMeta, iTutar - iMeta);
            Bosluksuz(arasi).Should().Contain(Bosluksuz("ceviri(\"b_siparis_toplami\")"),
                "tutardan ONCE, tutarin NE OLDUGUNU soyleyen etiket basilmali");

            // CIFT-ANLAM KIRICI: etiket KOSULSUZ. Etiket ile tutar arasina bir kosul
            // girerse kart bazen brut bazen net anlaminda okunur.
            arasi.Should().NotContain("?",
                "etiket ile tutar arasinda KOSUL olamaz - etiket her siparis icin AYNI olmali");

            // Anahtar uc dilde de tanimli olmali (T + AR birer; T ciftinin ikinci elemani EN).
            AnkrajliSayim(Index, "b_siparis_toplami").Should().Be(2,
                "yeni anahtar T ve AR sozluklerinde BIRER kez tanimli olmali");
        }

        // ── P-V5 (MANTIK-FIX-4 / K5): SUNUCU HATASINI CEVIREN TEK MERKEZ ────────────────
        // Ayni katlama zinciri IKI YERDE yaziliydi (`sifreHatasiniCevir` + wireAccount
        // icindeki ADSIZ kopya) ve ikisi de `wireAccount()` KAPSAMINDA hapisti; misafir
        // checkout onlara ULASAMIYOR ve sunucunun HAM TURKCE metnini basiyordu.
        // Bu pin (a) merkezin TEK oldugunu, (b) 500/429 kararlarinin capa aramasindan ONCE
        // verildigini, (c) bilinmeyen mesajda HAM BASIM statukosunun korundugunu tutar.
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti muhurdeki uc dilli
        // temsilci-hata olcumu ve bilinmeyen-mesaj simulasyonudur.
        [Fact]
        public void KAYNAK_SOZLESMESI_HataEslemesi_TEK_MERKEZDE_ve_500_429_CAPASIZ()
        {
            var b = YorumlariAyikla(Oku("frontend/api-bridge.js"));

            // (1) KATLAMA ZINCIRI TAM 1 KEZ - iki eski kopya SOKULMUS olmali.
            var zincirSayi = Regex.Matches(b, Regex.Escape("[şŞ]")).Count;
            zincirSayi.Should().Be(1,
                "Turkce harf katlama zinciri TEK yerde olmali - ayni kuralin ikinci kopyasi "
                + "bu depoda defalarca bedeli odenmis bir siniftir");
            b.Should().NotContain("function sifreHatasiniCevir",
                "kapsam hapsindeki eski yardimci SOKULMUS olmali");

            // (2) MERKEZ, `ceviri` ile AYNI DUZEYDE (IIFE ust duzeyi) tanimli olmali:
            // wireAccount govdesinin ICINDE olsaydi misafir checkout yine ULASAMAZDI.
            var iCeviri = b.IndexOf("function ceviri(", StringComparison.Ordinal);
            var iMerkez = b.IndexOf("function hataAnahtari(", StringComparison.Ordinal);
            var iWire = b.IndexOf("function wireAccount(", StringComparison.Ordinal);
            iCeviri.Should().BeGreaterThan(-1, "ceviri() bulunmali");
            iMerkez.Should().BeGreaterThan(-1, "esleme merkezi bulunmali");
            iWire.Should().BeGreaterThan(-1, "wireAccount bulunmali");
            iMerkez.Should().BeLessThan(iWire,
                "merkez wireAccount'tan ONCE, yani onun kapsami DISINDA tanimlanmali");

            var merkez = FonksiyonGovdesi(b, "function hataAnahtari(e)");

            // (3) 500 ve 429 KARARLARI CAPA DONGUSUNDEN ONCE. 500 yolu RFC 7807 doner ve
            // `message` alani YOKTUR - orada capa aramak YAPISAL OLARAK bosunadir.
            var i500 = merkez.IndexOf("500", StringComparison.Ordinal);
            var i429 = merkez.IndexOf("429", StringComparison.Ordinal);
            var iDongu = merkez.IndexOf("HATA_CAPALARI.length", StringComparison.Ordinal);
            i500.Should().BeGreaterThan(-1, "500+ dali bulunmali");
            i429.Should().BeGreaterThan(-1, "429 dali bulunmali");
            iDongu.Should().BeGreaterThan(-1, "capa dongusu bulunmali");
            i500.Should().BeLessThan(iDongu, "500 karari capa aramasindan ONCE verilmeli");
            i429.Should().BeLessThan(iDongu, "429 karari capa aramasindan ONCE verilmeli");

            // (4) 429 SEBEP IDDIASIZ: uc ayri kaynaktan gelir (guard, Redis rate-limit,
            // yerlesik limiter), bu yuzden "cok fazla acik siparisin var" gibi bir sebep
            // ATFEDILEMEZ.
            merkez.Should().Contain("h_rate_limit", "429 icin notr anahtar verilmeli");
            AnkrajliSayim(Index, "h_rate_limit").Should().Be(2,
                "notr 429 anahtari T ve AR sozluklerinde BIRER kez tanimli olmali");

            // (5) VAKUM KIRICI: capa tablosu GERCEKTEN dolu olmali - bos bir tablo
            // "merkez tek" iddiasini bedava dogru yapardi.
            var tablo = b.Substring(b.IndexOf("var HATA_CAPALARI", StringComparison.Ordinal));
            tablo = tablo.Substring(0, tablo.IndexOf("];", StringComparison.Ordinal));
            Regex.Matches(tablo, Regex.Escape("\", \"")).Count.Should().BeGreaterThan(20,
                "capa tablosu C listesinin bilinen kumesini tasimali");

            // (6) DIYAKRITIKSIZ VARYANT: sunucunun kendi metinlerinden en az biri
            // (Messages.cs "Kapida odeme limiti asildi") DIYAKRITIKSIZ yazilmis. Capa
            // katlanmis bicimde tutuldugu icin iki yazim da AYNI sonucu verir.
            tablo.Should().Contain("kapida odeme limiti asildi",
                "diyakritiksiz yazilan sunucu metni de capa kumesinde olmali");

            // (7) BILINMEYEN MESAJDA HAM BASIM STATUKO: misafir yolunda `e.message`
            // yedegi DURMALI - uydurma notr metin, sunucunun somut sebebinin YERINE GECMEZ.
            Bosluksuz(b).Should().Contain(Bosluksuz("_ha ? ceviri(_ha) : (e.message"),
                "bilinen hatada ceviri, bilinmeyende HAM metin basilmali");
        }

        // ── P-V6 (MANTIK-FIX-4 / K6): BELGE YONU AR'DA RTL, TEK KAYNAKTAN ───────────────
        // Dosyada `[dir="rtl"]` ile baslayan ON IKI KURAL ILK COMMIT'ten beri VARDI ama
        // HEPSI OLU KODDU: `dir` UC AYRI YERDE 'ltr'e SABITLENMISTI (acilis betigi - AR
        // dalinda ACIKCA -, setLang, dil gostergesi). `git log -S`: `setAttribute('dir',
        // 'rtl')` HICBIR COMMIT'te gecmiyor. Yazar RTL destegini yazmis ama ACMAMIS.
        // Bu pin (a) yonun TEK KAYNAKTAN turedigini, (b) 'ltr' sabitlemesinin geri
        // gelmedigini, (c) `dir=rtl` acilinca MASAUSTUNU KIRAN medya-kapsam asimetrisinin
        // kapali oldugunu tutar.
        // KISIT-1 (FrontendDokunmaHedefiTests:1039): setLang govdesindeki her fonksiyon
        // cagrisi index.html'de `function X(` ile TANIMLI olmali - yardimci bu yuzden
        // api-bridge'e DEGIL index.html'e konuldu.
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti muhurdeki koordinat
        // olcumleridir (1280px: filtre paneli 1205..1463 -> 937..1195; sortbox 753..845
        // -> 71..163; body text-align start -> right).
        [Fact]
        public void KAYNAK_SOZLESMESI_BelgeYonu_ARda_RTL_ve_TEK_KAYNAKTAN()
        {
            var ham = Index;
            var s = YorumlariAyikla(ham);

            // (1) TEK KAYNAK: yardimci index.html'de TANIMLI (KISIT-1) ve UC noktadan
            // cagriliyor -> tanim 1 + cagri 2 = en az 3 gecis.
            s.Should().Contain("function dvsYonUygula(",
                "yon yardimcisi index.html'de tanimli olmali - setLang pini (:1039) "
                + "api-bridge'te tanimlanan bir yardimciyi KABUL ETMEZ");
            Regex.Matches(s, Regex.Escape("dvsYonUygula")).Count.Should().BeGreaterThan(2,
                "yardimci TANIMLI olmakla kalmayip CAGRILMIS da olmali");

            // (2) 'ltr' SABITLEMESI GERI GELEMEZ. Acilis betigindeki AR dali da dahil.
            Bosluksuz(s).Should().NotContain(Bosluksuz("setAttribute('dir','ltr')"),
                "yon kosulsuz 'ltr' yazilarak sabitlenemez - uc noktanin ucu de yardimciya "
                + "bagli olmali");
            Bosluksuz(s).Should().Contain(Bosluksuz("setAttribute('dir','rtl')"),
                "acilis betigi AR dalinda 'rtl' yazmali (sayfa ilk boyandan DOGRU yonde)");

            // (3) CIFT-ANLAM KIRICI: yon AR'a BAGLI olmali. "Her zaman rtl" ya da "her
            // zaman ltr" uygulamasi (1) ve (2)'yi gecerdi.
            var yardimci = s.Substring(s.IndexOf("function dvsYonUygula(", StringComparison.Ordinal));
            yardimci = yardimci.Substring(0, yardimci.IndexOf('\n'));
            Bosluksuz(yardimci).Should().Contain(Bosluksuz("'ar'?'rtl':'ltr'"),
                "yon YALNIZ AR'da rtl olmali - TR ve EN ltr kalmali");

            // (4) MEDYA KAPSAM ASIMETRISI KAPALI: taban `.filter-side` kurali
            // @media(max-width:900px) ICINDE; RTL override'i DISINDA kalirsa masaustunde
            // taban uygulanmadan override uygulanir ve panel EKRAN DISINA itilir.
            // OLCULEN ONCE-DURUM (1280px): LTR 71..329 -> RTL 1205..1463.
            var iMedya = s.IndexOf("@media(max-width:900px){\n  [dir=\"rtl\"] .filter-side",
                StringComparison.Ordinal);
            iMedya.Should().BeGreaterThan(-1,
                "[dir=rtl] .filter-side override'lari @media(max-width:900px) ICINDE olmali");

            // (5) K6'nin kapattigi kirici kalemler kaynakta GERI GELEMEZ.
            s.Should().NotContain(".sortbox{margin-left:auto}",
                "fiziksel margin-left RTL'de yanlis tarafi doldurur (olculdu: 753..845, "
                + "dogru ayna 71..163) - margin-inline-start kullanilmali");
            s.Should().Contain("[dir=\"rtl\"] .toast.t-err{box-shadow:inset -4px",
                "toast tip seridi RTL'de KARSI kenara gecmeli");
            s.Should().Contain("[dir=\"rtl\"] .a11y-sw.on::after{transform:translateX(-18px)}",
                "erisilebilirlik anahtarinin topu RTL'de karsi uca gitmeli");
            YorumlariAyikla(Oku("frontend/api-bridge.js")).Should().NotContain("text-align:left",
                "yon-duyarli inline style `start` olmali (dosyanin fatura renderer'i ZATEN oyle)");

            // (6) VAKUM KIRICI: RTL kural kumesi GERCEKTEN dolu olmali - bos bir kume
            // "yon acildi" iddiasini anlamsiz kilardi.
            // MK-4b DENETIM DUZELTMESI (BULGU-1): olcut GECIS degil SATIR sayar. Gecis
            // sayimi ZEMINDE de 20 idi (bir satirda birden cok secici var), yani K6'nin
            // TUM override'lari geri alinsa bile esigi gecerdi - kirici DEGILDI. Satir
            // bazinda zemin 12, K6 sonrasi 17.
            var rtlSatir = s.Split('\n').Count(x => x.Contains("[dir=\"rtl\"]", StringComparison.Ordinal));
            rtlSatir.Should().BeGreaterThan(12,
                "RTL kural kumesi ZEMINDEKI on iki kurali VE K6'nin ekledigi override "
                + "satirlarini tasimali (zemin 12 satir -> K6 sonrasi 17)");
        }

        // ── P-V3 (MANTIK-FIX-4 / K3): CEKMECE ETIKETI TOPLADIGI SEYI ADLANDIRIR ────────
        // MK-4b DENETIM DUZELTMESI (BULGU-2): K3 tek basina PINSIZ kalmisti - etiket
        // sessizce `total`a donerse suit yesil kalirdi.
        // Cekmecede toplanan sey urun satirlari (ETKIN fiyatla) eksi kupon indirimi;
        // KARGO hicbir dalda eklenmiyor, MAGAZA KREDISI dusulmuyor. Bitisikteki odeme
        // paneli ise "Toplam"i KARGO DAHIL gosteriyor - ayni kelime iki ekranda IKI
        // FARKLI buyuklugu adlandiriyordu.
        // DURUST ETIKET: KAYNAK SOZLESMESI pinidir. Davranis kaniti muhurdeki uc dilli
        // olcumdur ("Toplam/Total/الإجمالي" -> "Ara Toplam/Subtotal/المجموع الفرعي").
        [Fact]
        public void KAYNAK_SOZLESMESI_CekmeceEtiketi_ARA_TOPLAM_ve_b_toplam_DEGERI_KORUNUR()
        {
            var s = YorumlariAyikla(Index);

            // Cekmece satiri `subtotal` anahtarini kullanmali.
            Bosluksuz(s).Should().Contain(Bosluksuz("class=\"cart-total\"><span>'+t('subtotal')"),
                "cekmece etiketi topladigi seyi adlandirmali - kargo ve kredi HARIC bir ara toplam");
            s.Should().NotContain("t('total')",
                "cekmecenin eski `total` anahtari geri gelmemeli (kargoyu IMA ederdi)");

            // VAKUM KIRICI: `subtotal` sozlukte T ve AR'da tanimli olmali - olmayan bir
            // anahtara gecmek ekranda HAM ANAHTAR gosterirdi.
            AnkrajliSayim(Index, "subtotal").Should().Be(2,
                "`subtotal` T ve AR sozluklerinde BIRER kez tanimli olmali");

            // CIFT-ANLAM KIRICI: `b_toplam` DOKUNULMAZ - o api-bridge'in PAYLASILAN
            // anahtari (dort cagiran) ve odeme panelinde KARGO DAHIL toplami adlandiriyor.
            // Degeri degistirilirse iki ekran yine ayni kelimeyi paylasirdi.
            s.Should().Contain("b_toplam:[\"Toplam\",\"Total\"]",
                "b_toplam'in DEGERI degistirilmemeli - K3 yalniz CEKMECE etiketini degistirir");
            Regex.Matches(YorumlariAyikla(Oku("frontend/api-bridge.js")),
                Regex.Escape("ceviri(\"b_toplam\")")).Count.Should().Be(4,
                "b_toplam'in dort cagirani KORUNMALI");
        }

        // ── P-V7 (MANTIK-FIX-4 / K7): TELEFON KURALININ DORT KOPYASI AYRISAMAZ ──────────
        // Ayni telefon regex'i DORT validator'da yaziliydi ve MANTIK-FIX-3'te "ayrisma
        // SIFIR" olarak olculmustu - ama o olcumu KORUYAN hicbir sey yoktu. Bu depoda
        // "ayni kuralin ikinci kopyasi" sinifinin bedeli ALTI KEZ odendi (B10 · D5 · K7/
        // Faz-0 · D-SEMA · MFIX-3b cift `sepetImzasi` · MF-4/K5 esleme kopyasi).
        // KAPSAM (E'nin olcumu): pin YALNIZ REGEX uzerinde kurulur. Mesaj metni
        // ("Gecerli bir telefon girin." vs "Gecerli telefon giriniz.") ve NotEmpty
        // kullanimi DORT SITEDE AYRISIYOR; "birebir ayni" diyen bir pin ILK KOSUMDA
        // kirmizi verirdi. O ayrisma rapora yazildi, pine GIRMEDI.
        // DOSYA LISTESI SABIT DEGIL, TARANIR: besinci bir kopya eklenirse pin onu da
        // gorur.
        // KACISSIZ: regex referans degeri pinde LITERAL yazilmaz (`\s` ve parantez bu
        // depoda yazim zincirinde IKI KEZ kayboldu) - dosyalardan cikarilan degerler
        // BIRBIRLERIYLE karsilastirilir, vakum kiricilar ayri tutulur.
        //
        // ══ GF-5 / K4 - PIN BILINCLI OLARAK DEGISTIRILDI (BOZDUKLARIM kaydi) ══════════════
        //
        // ESKI IDDIA: "telefon regex'i DORT validator'da LITERAL yazili ve dordu BIREBIR ayni".
        // K4 uc kopyayi `GirdiSinirlari.TelefonDeseni` sabitine bagladi, dolayisiyla artik
        // yalnizca `SellerRegisterRequestValidator` LITERAL tasiyor (Seller DOKUNULMAZ - bu
        // dalganin "Seller* enjeksiyon noktalarina 0 SATIR" siniri). Eski pin, `.Matches(@"`
        // bicimini saydigi icin K4 sonrasi 1 site bulup `BeGreaterThan(3)`te KIRILDI - yani
        // pin GERCEKTEN olcuyordu (bedava dogru degildi).
        //
        // KORUNAN SEY AYNI, OLCUM BICIMI DEGISTI: "ayni girdiyi bir uctan kabul edip
        // digerinden reddeden sessiz tutarsizlik OLUSAMAZ". Yeni pin her telefon kuralini
        // iki bicimde de kabul eder ve HEPSININ AYNI DEGERE cozundugunu dogrular:
        //   (a) `GirdiSinirlari.TelefonDeseni` REFERANSI  -> deger sabitten okunur
        //   (b) `@"..."` LITERALI                          -> deger satirdan okunur
        // Boylece besinci bir kopya (hangi bicimde olursa olsun) YINE yakalanir ve Seller'in
        // literali sabitten AYRISIRSA pin KIRMIZI verir.
        //
        // NOT (on olcum duzeltmesi): GF-5 kapsam elestirmeni "bu kopyalari koruyan HICBIR
        // TARAMA PINI YOK" demisti - OLCUM bunu YALANLADI: pin VARDI ve K4'te kirildi.
        [Fact]
        public void KAYNAK_SOZLESMESI_TelefonKurali_TUM_VALIDATORLERDE_AYNI_DEGERE_COZUNUR()
        {
            var dizin = Path.Combine(KokDizin.Value, "Divisima.Bussiness", "ValidationRules");
            Directory.Exists(dizin).Should().BeTrue("validator dizini bulunmali: " + dizin);

            // Sabitin GERCEK degeri kaynaktan okunur - pine LITERAL yazilmaz (kacis-kaybi).
            var sabitYol = Path.Combine(KokDizin.Value, "Divisima.Core", "Utilities", "Validation",
                "GirdiSinirlari.cs");
            File.Exists(sabitYol).Should().BeTrue("ortak sabit dosyasi bulunmali: " + sabitYol);
            var sabitSatir = File.ReadAllLines(sabitYol)
                .FirstOrDefault(s => s.Contains("TelefonDeseni", StringComparison.Ordinal)
                                     && s.Contains("@\"", StringComparison.Ordinal));
            sabitSatir.Should().NotBeNull("TelefonDeseni sabiti kaynakta bulunmali");
            var sb = sabitSatir!.IndexOf("@\"", StringComparison.Ordinal) + 2;
            var ss = sabitSatir.IndexOf('"', sb);
            var sabitDeger = sabitSatir.Substring(sb, ss - sb);

            var bulgu = new List<(string dosya, string deger, string bicim)>();
            foreach (var yol in Directory.GetFiles(dizin, "*.cs"))
            {
                var satirlar = File.ReadAllLines(yol);
                for (var i = 0; i < satirlar.Length; i++)
                {
                    var j = satirlar[i].IndexOf(".Matches(", StringComparison.Ordinal);
                    if (j < 0) continue;
                    // Telefon kurali mi: bu satirda ya da onceki iki satirda `phone` gecmeli.
                    var pencere = string.Join("\n", satirlar.Skip(Math.Max(0, i - 2)).Take(3));
                    if (pencere.IndexOf("phone", StringComparison.Ordinal) < 0) continue;

                    if (satirlar[i].Contains("GirdiSinirlari.TelefonDeseni", StringComparison.Ordinal))
                    {
                        // (a) REFERANS bicimi: deger TANIM GEREGI sabitin degeridir.
                        bulgu.Add((Path.GetFileName(yol), sabitDeger, "referans"));
                        continue;
                    }

                    var bas = satirlar[i].IndexOf('"', j);
                    if (bas < 0) continue;
                    var son = satirlar[i].IndexOf('"', bas + 1);
                    son.Should().BeGreaterThan(bas, "regex kapanis tirnagi bulunmali: " + yol);
                    // (b) LITERAL bicimi.
                    bulgu.Add((Path.GetFileName(yol), satirlar[i].Substring(bas + 1, son - bas - 1), "literal"));
                }
            }

            // VAKUM KIRICI 1: tarama GERCEKTEN dosya okumus ve dort siteyi bulmus olmali.
            bulgu.Count.Should().BeGreaterThan(3,
                "telefon kurali en az DORT validator'da bulunmali - tarama bos donerse "
                + "esitlik iddiasi BEDAVA dogru olurdu");

            // ══ GF-5 / F3 (B-4) - "IKI BICIM DE OLMALI" ASSERT'I GEVSETILDI ═══════════════
            // ONCEKI HALI `Distinct().HaveCount(2)` idi ve ILERI DONUK BIR TUZAKTI: Seller
            // modulu acilip literali de ortak sabite BAGLANIRSA - yani bir IYILESTIRME
            // yapilirsa - pin KIRMIZI verirdi. Bir pin, korudugu seyin DUZELTILMESINI
            // cezalandiramaz (MK-4b denetcisi yakaladi).
            //
            // DURUST KAYIT: asagidaki `<= 2` bu kod yolunda AYIRT EDICI DEGILDIR - `bicim`
            // yalnizca "referans" ya da "literal" degerini alabildigi icin Distinct sayisi
            // zaten {1,2} kumesindedir. Sozlesmeyi ACIK yazmak icin duruyor; VAKUM KIRICI
            // gorevini ALTTAKI assert devralir.
            bulgu.Select(x => x.bicim).Distinct().Count().Should().BeLessThanOrEqualTo(2,
                "yalnizca IKI bicim taninir: ortak sabite REFERANS ya da LITERAL");

            // ASIL AYIRT EDICI: literal tasiyan site sayisi ARTAMAZ. Bugun tam BIR tanedir
            // (SellerRegisterRequestValidator - DOKUNULMAZ). Yeni bir literal kopya eklenirse
            // bu assert KIRMIZI verir; Seller yarin sabite baglanirsa sayi 0'a duser ve pin
            // YESIL kalir - iyilestirme CEZALANDIRILMAZ, yeni kopya ise YAKALANIR.
            bulgu.Count(x => x.bicim == "literal").Should().BeLessThanOrEqualTo(1,
                "telefon deseninin LITERAL kopyasi ARTMAMALI - ortak sabite baglanmali "
                + "(bugun tek literal Seller'dadir ve o DOKUNULMAZ)");

            // VAKUM KIRICI 3: cikarilan deger gercekten bir telefon karakter sinifi olmali.
            // NOT (F3/B-5): niceleyici capasi ELLE YAZILMAZ - sabitten turer. Onceki hali
            // `Contain("{7,20}")` idi ve sabit degistiginde tani "niceleyici YOK" diyordu;
            // oysa gercek kusur "sabit DEGISTI"dir. Capa artik sabitin KENDISINDEN okunuyor,
            // yani tani her zaman dogru yone bakar.
            var niceleyici = System.Text.RegularExpressions.Regex.Match(sabitDeger, @"\{\d+,\d+\}").Value;
            niceleyici.Should().NotBeNullOrEmpty("ortak sabit bir uzunluk niceleyicisi tasimali");
            foreach (var (dosya, deger, _) in bulgu)
            {
                deger.Should().NotBeNullOrWhiteSpace("regex bos okunmus olamaz: " + dosya);
                deger.Should().Contain("0-9", "telefon kurali rakam sinifi tasimali: " + dosya);
                deger.Should().Contain(niceleyici,
                    "telefon kuralinin uzunluk niceleyicisi ortak sabitle AYNI olmali: " + dosya);
            }

            // ASIL IDDIA: TUM siteler AYNI DEGERE cozunmeli. Ihlalci dosya ADIYLA raporlanir.
            var ayrisan = bulgu.Where(x => !string.Equals(x.deger, sabitDeger, StringComparison.Ordinal))
                               .Select(x => x.dosya + " (" + x.bicim + ")").ToList();
            ayrisan.Should().BeEmpty(
                "telefon kuralinin TUM siteleri ortak sabitle AYNI degere cozunmeli. "
                + "Ayrisan bir kopya, ayni girdiyi bir uctan kabul edip digerinden "
                + "reddeden SESSIZ bir tutarsizlik uretir.");
        }
    }
}
