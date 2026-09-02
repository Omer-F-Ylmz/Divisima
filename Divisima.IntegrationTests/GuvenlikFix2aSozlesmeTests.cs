using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GUVENLIK-FIX-2a (GF-2a) - ISTEMCI KACIS SOZLESMESI ═══════════════════════════════
    //
    // NEDEN KAYNAK-SOZLESME PINI: bu depoda JS/DOM kosucusu YOK (AngleSharp/Jint/Playwright
    // bagimliligi acilmadi - `frontend/test/mobil-erisilebilirlik.js` basligi bunu yaziyor).
    // Davranis kaniti YALNIZCA muhurdeki tarayici olcumleridir; bu dosya o davranisin
    // KAYNAK KOSULLARINI sabitler ki sessizce geri alinmasin.
    //
    // KOK BAZLI: GF-2a'nin kapanis kumesi 24 kalem / 8 KOK idi. Pinler KOK basina kurulur -
    // 24 satir pini yazmak, bir satir kaydiginda pini kirilgan yapardi ve asil sozlesmeyi
    // (kural TEK YERDE) olcmezdi.
    //
    // MK-6 NOTU: her pin, korudugu alani ONCEKI haline donduren bir uretim mutasyonuyla
    // sinandi; sonuclar muhurde. "Kirmizi-once" tek basina yetmez cunku aranan dizge baska
    // baglamda da gecebilir - bu yuzden assertler ALAN BAZLI ve cift yonlu (kacisli desen
    // VAR + kacissiz desen YOK).
    public class GuvenlikFix2aSozlesmeTests
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

        // Bir JS fonksiyonunun govdesini suslu parantez sayarak cikarir (regex YOK - ic ice
        // objelerde regex sessizce yanlis keser). `AdminPanelSozlesmeTests`teki kalibin aynisi.
        private static string FonksiyonGovdesi(string kaynak, string fonksiyonAdi)
        {
            var i = kaynak.IndexOf("function " + fonksiyonAdi, StringComparison.Ordinal);
            i.Should().BeGreaterThan(-1, $"'{fonksiyonAdi}' fonksiyonu kaynakta bulunmali");
            var acilis = kaynak.IndexOf('{', i);
            acilis.Should().BeGreaterThan(-1);
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
            throw new InvalidOperationException($"'{fonksiyonAdi}' govdesinin kapanisi bulunamadi.");
        }

        private static int Sayim(string metin, string parca) =>
            metin.Split(parca).Length - 1;

        // ══ CAPA KIRLENMESI - YAPISAL COZUM (bu depoda ALTI KEZ dusuldu) ═══════════════
        //
        // "Kacissiz desen 0" turu NEG assertler, DUZELTMEYI ANLATAN YORUMUN taranan dizgeyi
        // METIN olarak tasimasi yuzunden defalarca yanlis kirmizi verdi. Her seferinde yorum
        // yeniden yazilarak cozuldu - yani insan disiplinine birakildi ve YINE dusuldu.
        // Bu yuzden NEG sayimlari YORUMSUZ kaynak uzerinde yapilir: `//` satir yorumlari,
        // `/* */` blok yorumlari ve HTML `<!-- -->` yorumlari SOKULUR.
        // (Ayni cozum `GuvenlikFix1SozlesmeTests.KodSatirlari`da da var - kalibin devami.)
        private static string KodSatirlari(string kaynak)
        {
            var s = System.Text.RegularExpressions.Regex.Replace(kaynak, "<!--.*?-->", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            return string.Join("\n", s.Split('\n')
                .Select(satir =>
                {
                    // "//" her zaman yorum DEGILDIR. Iki yanlis pozitif OLCULDU:
                    //   "https://..."  -> onceki karakter ':'
                    //   /^https?:\/\//i -> onceki karakter '\' (regex icinde KACISLI bolu)
                    // Ikisinde de kesme YAPILMAZ; aksi halde kod satiri yarim kalir ve POZ
                    // assertler sessizce kirilir (bu turda birebir yasandi).
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

        // Class metodu govdesi (`ad(...) {`). `FonksiyonGovdesi` yalniz `function ad` bicimini
        // bulur; `api-client.js` bir SINIF ve metotlari o bicimde DEGIL.
        private static string MetotGovdesi(string kaynak, string metotAdi)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                kaynak, @"(?m)^\s*(?:async\s+)?" + System.Text.RegularExpressions.Regex.Escape(metotAdi) + @"\s*\([^)]*\)\s*\{");
            m.Success.Should().BeTrue($"'{metotAdi}' metodu kaynakta bulunmali");
            var acilis = kaynak.IndexOf('{', m.Index + m.Length - 1);
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
            throw new InvalidOperationException($"'{metotAdi}' govdesinin kapanisi bulunamadi.");
        }

        // ── KOK-1 + KOK-2: RENK ve MARKA (D-3, D-2) ─────────────────────────────────────
        //
        // OLCULEN ONCE-DURUM: `ph()` HEM `col`u `style="--col:X"` icine HEM `brand`i <span>
        // icine HAM koyuyordu - iki kok TEK fonksiyondaydi.
        [Fact]
        public void GF2A_K2_K4_PH_MARKAYI_KACIRIR_ve_RENGI_ALLOWLISTTEN_GECIRIR()
        {
            var kaynak = Oku("frontend/index.html");
            var govde = FonksiyonGovdesi(kaynak, "ph");

            govde.Should().Contain("esc(brand)", "marka METIN baglamindadir - kacisli yazilmali");
            govde.Should().NotContain("+brand+", "ham marka yazimi GERI GELMEMELI");
            govde.Should().Contain("guvenliRenk(col)", "renk NITELIK baglamindadir - allowlist'ten gecmeli");
            govde.Should().NotContain("(col||'#d9cfc2')", "ham renk yazimi GERI GELMEMELI");

            // Allowlist BACKEND ile AYNI KUMEYI kabul etmeli: ProductAddRequestValidator
            // `[0-9a-fA-F]` kullaniyor; kucuk-harf-only bir desen GECERLI veriyi reddederdi.
            var renkGovde = FonksiyonGovdesi(kaynak, "guvenliRenk");
            renkGovde.Should().Contain("_HEX_RE", "hex deseni TEK sabitten gelmeli");
            kaynak.Should().Contain("0-9a-fA-F", "desen BUYUK HARFI de kabul etmeli (backend kumesi)");
            // 5/7 hane GECERSIZ CSS'tir; desen yalniz 3/4/6/8 haneye izin vermeli.
            kaynak.Should().NotContain("[0-9a-fA-F]{3,8}", "acik aralik 5 ve 7 haneyi de gecirirdi");
        }

        // ── KOK-3: GORSEL URL SEMA ALLOWLIST'I (D-4) ────────────────────────────────────
        //
        // Politika TEK YERDE: `api-client.js resolveUrl`. Render katmaninda ikinci kopya YOK -
        // alti cagri yeri (imgFill/media/thumb/thumbC + 2 dogrudan) ORADAN geciyor.
        [Fact]
        public void GF2A_K3_RESOLVEURL_SEMA_ALLOWLISTI_TEK_YERDE()
        {
            var kaynak = Oku("frontend/api-client.js");
            // POZ assertler HAM govdede: bu metot regex ve dize literallerinde `//` tasiyor
            // (`/^https?:\/\//i`, `u.startsWith("//")`) ve KABA bir yorum siyirici bunlari
            // yorum sanip kesiyor - OLCULDU, iki ayri assert bu yuzden yanlis kirmizi verdi.
            // Siyirici YALNIZ "kacissiz desen 0" turu NEG sayimlarda kullanilir; orada
            // korudugu deger (yorumun capayi kirletmesi) gercek ve bedeli odenmis.
            var govde = MetotGovdesi(kaynak, "resolveUrl");

            // KABUL yolu - capalar HAM KAYNAKTAN kopyalandi (MK-7)
            govde.Should().Contain(@"/^https?:\/\//i.test(u)", "mutlak http(s) KABUL edilmeli");
            govde.Should().Contain(@"/^data:image\/(png|jpeg|jpg|gif|webp);base64,/i",
                "yalniz RASTER goruntu + base64 KABUL - SVG script tasir, disarida kalmali");

            // RED yolu - ayirt edici: eski kosul her `data:`yi ve `//` onekini geciriyordu.
            govde.Should().NotContain(@"|| /^data:/i.test(u)) return u",
                "eski GENIS data: kabulu GERI GELMEMELI");
            govde.Should().Contain(@"u.startsWith(""//"")", "protokol-goreli URL REDDEDILMELI");
            govde.Should().Contain(@"/^[a-z][a-z0-9+.-]*:/i", "sema tasiyan her sey REDDEDILMELI");

            // IKINCI KOPYA NEG KONTROLU: render katmanlari kendi sema suzgecini ACMAMALI.
            foreach (var yol in new[] { "frontend/index.html", "frontend/api-bridge.js" })
                Sayim(KodSatirlari(Oku(yol)), @"data:image\/(png").Should().Be(0,
                    $"{yol} sema politikasinin IKINCI KOPYASINI tasimamali - politika resolveUrl'de");
        }

        // ── KOK-4: HATA MESAJI / TOAST (D-5) ────────────────────────────────────────────
        //
        // Duz `textContent` IKI SEYI KIRARDI: ikon <span>'i ve GERI AL butonu. Bu yuzden
        // ISKELET DOM: yapisal parcalar createElement, YALNIZ mesaj metin dugumu.
        [Fact]
        public void GF2A_K5_TOAST_ISKELET_DOM_ve_GERI_AL_BUTONU_YASIYOR()
        {
            var kaynak = Oku("frontend/index.html");

            var toastGovde = FonksiyonGovdesi(kaynak, "_toastStep");
            toastGovde.Should().NotContain("toastEl.innerHTML=", "mesaj artik innerHTML'e GIRMEMELI");
            toastGovde.Should().Contain("createTextNode", "mesaj METIN DUGUMU olarak konmali");

            var undoGovde = FonksiyonGovdesi(kaynak, "toastUndo");
            undoGovde.Should().NotContain("toastEl.innerHTML=", "mesaj artik innerHTML'e GIRMEMELI");
            undoGovde.Should().Contain("createElement('button')",
                "GERI AL butonu KORUNMALI - duz textContent onu OLDURURDU");
            undoGovde.Should().Contain("createTextNode", "mesaj METIN DUGUMU olarak konmali");
            // Butonu bulan satir HALA calisiyor olmali (ozelligin kendisi).
            undoGovde.Should().Contain(".toast-undo", "geri-al kancasi yerinde kalmali");

            // CIFT KACIS NEG KONTROLU: cagri yerleri artik on-kacis YAPMAMALI, aksi halde
            // ekranda "&amp;" gorunurdu.
            Sayim(kaynak, "toast(esc(").Should().Be(0,
                "sink artik kacis yapmiyor - cagri yerindeki on-kacis CIFT KACIS uretirdi");
        }

        // ── KOK-5 + KOK-8: KUPON KODU ve SEPET BEDENI (D-10) ────────────────────────────
        [Fact]
        public void GF2A_K6_KOK8_KUPON_KODU_ve_SEPET_BEDENI_KACISLI()
        {
            var kaynak = Oku("frontend/index.html");
            kaynak.Should().Contain("' ('+esc(coupon.code)+')", "kupon kodu kacisli yazilmali");
            Sayim(kaynak, "' ('+coupon.code+')").Should().Be(0, "ham kupon kodu GERI GELMEMELI");

            var meta = FonksiyonGovdesi(kaynak, "cartMeta");
            meta.Should().Contain("esc(String(it.size))", "sepet bedeni kacisli yazilmali");
            meta.Should().NotContain("' '+it.size)", "ham beden yazimi GERI GELMEMELI");
        }

        // ── KOK-6: KATEGORI ETIKETI SABIT DEGIL, VERITABANI METNI ───────────────────────
        //
        // Bu turun EN ONEMLI YENI bulgusu: `kategoriEtiketiKaydet` (api-bridge.js) DB'deki
        // `c.name`i sozluge YAZIYOR, dolayisiyla `t('cat_*')` HAM DB METNI dondurur. Kaynak
        // okuyana "sozluk cagrisi = SABIT" gorunur; DEGILDIR.
        // SOZLUGE ve `kategoriEtiketiKaydet`e DOKUNULMADI (i18n dokunulmaz) - kacis SINK'te.
        [Fact]
        public void GF2A_KOK6_KATEGORI_ETIKETI_ve_SLUG_SINKTE_KACIRILIR()
        {
            var index = Oku("frontend/index.html");
            var bridge = Oku("frontend/api-bridge.js");

            // Enjeksiyon mekanizmasi HALA YERINDE olmali - onu sokmek i18n'i bozardi.
            bridge.Should().Contain("kategoriEtiketiKaydet",
                "sozluk enjeksiyonu KALDIRILMADI - kacis sink tarafinda yapiliyor");

            // Etiket cikisi kacisli
            foreach (var desen in new[] { "esc(t('cat_'+c[0]))", "esc(t('cat_'+item.slug))",
                                          "esc(t('sub_'+s.slug))", "esc(t('cat_'+n.slug))" })
                index.Should().Contain(desen, $"kategori etiketi kacisli yazilmali: {desen}");

            // Ham yazimlar GERI GELMEMELI (alan bazli NEG kontrol)
            foreach (var ham in new[] { "+t('cat_'+c[0])+", "+t('cat_'+item.slug)+",
                                        "+t('sub_'+s.slug)+", "+t('cat_'+n.slug)+" })
                Sayim(index, ham).Should().Be(0, $"ham etiket yazimi GERI GELMEMELI: {ham}");

            // Slug NITELIK baglaminda - o da DB kaynakli
            Sayim(index, "href=\"#/kategori/'+esc(").Should().BeGreaterThan(0,
                "kategori baglantisindaki slug kacisli olmali");
            Sayim(index, "href=\"#/kategori/'+item.slug+'").Should().Be(0, "ham slug GERI GELMEMELI");
            Sayim(index, "href=\"#/kategori/'+c[0]+'").Should().Be(0, "ham slug GERI GELMEMELI");
            Sayim(index, "href=\"#/kategori/'+n.slug+'").Should().Be(0, "ham slug GERI GELMEMELI");

            bridge.Should().Contain("esc(n.slug)", "api-bridge yarisindaki slug da kacisli olmali");
        }

        // ── KOK-7: ADMIN ALANLARI + JS BAGLAMI ──────────────────────────────────────────
        [Fact]
        public void GF2A_K7_ADMIN_ALANLARI_KACISLI_ve_INLINE_JS_KALKTI()
        {
            var admin = Oku("frontend/admin.html");

            admin.Should().Contain("${esc(p.product_name)}", "urun adi kacisli olmali");
            Sayim(admin, "${p.product_name}").Should().Be(0, "ham urun adi GERI GELMEMELI");

            // IKIZ: :432 hamdi, :476 kacisliydi - celiskili cift kapatildi.
            Sayim(admin, "${o.order_number||o.id}").Should().Be(0,
                "ham order_number GERI GELMEMELI - kacisli ikizi ZATEN vardi");

            admin.Should().Contain("(${esc(r.size)})", "iade satirindaki beden kacisli olmali");

            // SUPHE-2: HTML kacisi JS baglaminda COZULUYORDU (&#39; -> ').
            // NEG sayim YORUMSUZ kaynak uzerinde - duzeltmeyi anlatan yorum eski bicimi
            // METIN olarak tasiyor (capa kirlenmesi, bu depoda alti kez dusuldu).
            Sayim(KodSatirlari(admin), "onclick=\"pickSize('").Should().Be(0,
                "satir ici JS baglamina kacisli deger GOMULMEMELI - data-* + addEventListener");
            admin.Should().Contain("data-pick-size=", "deger data-* niteliginde tasinmali");
            admin.Should().Contain("b.dataset.pickSize", "JS'e dataset uzerinden gecmeli");
        }

        // ── K1: PANELDE FAIL-CLOSED SANITIZER ───────────────────────────────────────────
        //
        // DURUST SINIR (raporda da yazili): panelde BUGUN sunucudan gelen HTML cizen bir
        // yuzey YOK. Bu sarmalayici bir kusuru KAPATMIYOR; storefront'takiyle AYNI sozlesmeyi
        // panele tasiyor ki HTML cizen bir yuzey eklendiginde fail-closed HAZIR olsun.
        [Fact]
        public void GF2A_K1_ADMIN_PURIFY_VENDORDAN_ve_SARMALAYICI_FAIL_CLOSED()
        {
            var admin = Oku("frontend/admin.html");

            admin.Should().Contain("/vendor/purify.min.js",
                "purify YEREL vendor'dan yuklenmeli - CDN'e YENI bagimlilik ACILMAZ");

            // SIRA: purify, api-client'tan ONCE. Sonra gelseydi ilk cizimde fail-closed
            // sarmalayici icerigi DUSURURDU.
            var purifyIdx = admin.IndexOf("/vendor/purify.min.js", StringComparison.Ordinal);
            var clientIdx = admin.IndexOf("src=\"api-client.js\"", StringComparison.Ordinal);
            purifyIdx.Should().BeGreaterThan(-1);
            clientIdx.Should().BeGreaterThan(-1);
            purifyIdx.Should().BeLessThan(clientIdx, "purify api-client'tan ONCE yuklenmeli");

            var govde = FonksiyonGovdesi(admin, "guvenliHTML");
            govde.Should().Contain("typeof window.DOMPurify === \"undefined\"",
                "purify yoksa FAIL-CLOSED olmali");
            govde.Should().Contain("return null", "fail-closed yolu null donmeli");
            govde.Should().Contain("ALLOWED_URI_REGEXP", "URI semasi kisitlanmali");

            var yaz = FonksiyonGovdesi(admin, "guvenliYaz");
            yaz.Should().Contain("temiz === null", "null gelince HAM HTML BASILMAMALI");
            yaz.Should().Contain("textContent", "null durumunda metin yazilmali");
        }

        // ── K8: SERVICE WORKER IKI KOVA + /api/ NETWORK-ONLY ────────────────────────────
        [Fact]
        public void GF2A_K8_SW_API_ONBELLEGE_YAZMAZ_ve_IKI_KOVA_VAR()
        {
            var swHam = Oku("frontend/service-worker.js");
            var sw = KodSatirlari(swHam);

            sw.Should().Contain("const API_CACHE", "API icin AYRI kova olmali");
            sw.Should().Contain("const CACHE = \"divisima-shell-", "kabuk kovasi AYRI adlandirilmali");

            // `/api/` dali artik respondWith KURMAMALI - eski hal `caches.open(CACHE).put`ti.
            var apiDali = sw.Substring(sw.IndexOf("url.pathname.startsWith(\"/api/\")", StringComparison.Ordinal));
            var daliSonu = apiDali.IndexOf('}');
            apiDali.Substring(0, daliSonu).Should().NotContain("caches.open",
                "API yaniti ONBELLEGE YAZILMAMALI - Cache Storage `no-store` basligini UYGULAMAZ");
            apiDali.Substring(0, daliSonu).Should().NotContain("respondWith",
                "API dali network-only olmali - SW araya GIRMEMELI");

            // Cikis kancasi: YALNIZ api kovasi silinir, kabuk kovasi DURUR (offline acilis).
            sw.Should().Contain("caches.delete(API_CACHE)", "cikista API kovasi silinmeli");
            Sayim(sw, "caches.delete(CACHE)").Should().Be(0,
                "KABUK kovasi silinmemeli - offline acilis KORUNUR");

            // Capraz-origin kapisi: opak kopya SRI'yi dusurebilir.
            sw.Should().Contain("url.origin !== self.location.origin",
                "capraz-origin isteklere SW dokunmamali");

            // ISTEMCI YARISI: kanca GERCEKTEN cagriliyor olmali - yoksa SW dinleyicisi OLU
            // kod olurdu (vakum). Cikis TEK NOKTADAN gecmedigi icin kanca `setAccessToken`
            // icine kondu: jeton null'lanmasi "oturum bitti"nin tek guvenilir isaretidir.
            var client = KodSatirlari(Oku("frontend/api-client.js"));
            client.Should().Contain("type: \"divisima-logout\"",
                "istemci cikista SW'ye mesaj GONDERMELI - aksi halde SW kancasi OLU kod olur");
        }

        // ── K9: SRI - integrity sayisi = UZAK script sayisi ─────────────────────────────
        [Fact]
        public void GF2A_K9_UZAK_SCRIPT_INTEGRITY_TASIR_FONTLARA_EKLENMEZ()
        {
            var admin = Oku("frontend/admin.html");
            var index = Oku("frontend/index.html");

            // Depodaki TEK uzak script Chart.js; surume PINLI oldugu icin sabit hash gecerli.
            admin.Should().Contain("chart.js@4.4.1", "Chart.js surume PINLI kalmali");
            admin.Should().Contain("integrity=\"sha384-", "uzak script SRI tasimali");
            admin.Should().Contain("crossorigin=\"anonymous\"", "SRI icin CORS gerekli");

            // NEG KONTROL - KABUL EDILMIS RISK: Google Fonts `css2` yaniti User-Agent'a gore
            // DEGISIR, sabit hash YOKTUR; integrity eklemek SITEYI KIRARDI.
            var fontSatirlari = index.Split('\n').Where(s => s.Contains("fonts.googleapis.com")).ToList();
            fontSatirlari.Should().NotBeEmpty("font baglantilari yerinde olmali");
            fontSatirlari.Should().OnlyContain(s => !s.Contains("integrity="),
                "Google Fonts CSS'ine SRI EKLENMEZ - yanit UA'ya gore degisir (kabul edilmis risk)");
        }

        // ── K10: SEKMELER ARASI TEK REFRESH ─────────────────────────────────────────────
        //
        // Sunucu tarafi (GF-1b/K4) ayni refresh jetonunun iki kez sunulmasini YENIDEN KULLANIM
        // sayip TUM oturum zincirini iptal ediyor. Istemci esgudumsuzse bu savunma kullaniciya
        // KARSI calisir: iki sekme = iki refresh = herkes cikar.
        [Fact]
        public void GF2A_K10_REFRESH_ORIGIN_GENELINDE_TEK_ve_AG_GOVDESI_TEK()
        {
            var kaynak = Oku("frontend/api-client.js");

            // Capa HAM KAYNAKTAN: cagri iki satira bolunmus (`navigator.locks` / `.request(...)`),
            // tek parca arama KACIRIRDI (MK-7 - eslesme-bicimi-farki ailesi).
            kaynak.Should().Contain(".request(\"divisima-refresh\"",
                "refresh ORIGIN GENELINDE kilitlenmeli - sekmeler arasi esgudum");
            kaynak.Should().Contain("navigator.locks", "kilit primitifi navigator.locks olmali");

            // FAIL-SAFE: destek yoksa ORNEK-ICI single-flight'a duser (davranis eskisiyle ayni).
            kaynak.Should().Contain("navigator.locks && navigator.locks.request",
                "destek YOKLUGUNDA mevcut single-flight'a dusmeli - KIRILMAMALI");
            kaynak.Should().Contain("if (this._refreshing) return this._refreshing;",
                "ornek-ici single-flight KORUNMALI");

            // AG GOVDESI TEK: kilit eklenirken govde KOPYALANMADI, CIKARILDI.
            Sayim(kaynak, "/api/auth/refresh\", {").Should().Be(1,
                "refresh AG CAGRISI TEK GOVDEDE olmali - ikinci kopya ACILMAMALI");
            kaynak.Should().Contain("async _refreshAgCagrisi()", "ag govdesi ayri metotta olmali");

            // `finally` temizligi: ilk basarisizliktan sonra KALICI bozulma olmamali.
            Sayim(kaynak, "this._refreshing = null;").Should().BeGreaterThan(1,
                "kilitli ve kilitsiz yolun IKISI de bekleyen promise'i temizlemeli");
        }
    }
}
