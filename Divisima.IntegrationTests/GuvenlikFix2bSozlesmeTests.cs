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

        // ══ K3 - 429 AYRI HATA SINIFI ══════════════════════════════════════════════════
        //
        // 429 "istek YANLIS" demek degil, "SIMDI olmaz" demektir. Istemci bu ayrimi
        // yapmadigi icin gecici bir limit KALICI olumsuz sonuca donusuyordu.
        [Fact]
        public void GF2B_K3_HIZ_LIMITI_AYRI_HATA_SINIFI_ve_STATUS_GERIYE_UYUMLU()
        {
            var kaynak = Oku("frontend/api-client.js");

            kaynak.Should().Contain("class DivisimaRateLimitError extends Error",
                "429 icin AYRI hata sinifi bulunmali - cagiran ayrimi tip duzeyinde yapabilmeli");

            var parse = MetotGovdesi(kaynak, "async _parse(res)");
            parse.Should().Contain("res.status === 429",
                "yanit ayristirmasi 429'u AYRI dala almali");
            parse.Should().Contain("throw new DivisimaRateLimitError(",
                "429 yolunda genel Error DEGIL, hiz limiti sinifi firlatilmali");

            // ══ GERIYE UYUMLULUK - CIFT ANLAM KIRICI ═══════════════════════════════════
            // Yeni sinif mevcut `e.status` okuyucularinin YERINE degil USTUNE gelir.
            // `status` dusseydi kupon ve arama dallari 429'u 0 (=ulasilamadi) sanardi -
            // tesadufen dogru sonuc, ama YANLIS SEBEPLE; sonraki bir degisiklikte kirilirdi.
            kaynak.Should().Contain("this.status = 429;",
                "sinif `status` alanini KORUMALI - mevcut e.status okuyuculari kirilmamali");
            kaynak.Should().Contain("global.DivisimaRateLimitError = DivisimaRateLimitError;",
                "sinif disari verilmeli ki cagiran `instanceof` ile de ayirt edebilsin");
        }

        // ══ K3/b - ARAMADA 429 ONBELLEGE YAZILMAZ ══════════════════════════════════════
        //
        // OLCULEN KIRIK (goz turu): arama ucu 429 aldi, ekranda "Sonuc bulunamadi" yazdi.
        // Sebep: catch HER hatayi `{ q, items: [] }` ile BASARILI-AMA-BOS sonuc olarak
        // onbellege yaziyordu ve `e.status` HIC okunmuyordu. Onbellek yazildigi icin limit
        // GECTIKTEN SONRA bile ayni sorgu bos sonuc gosteriyordu.
        [Fact]
        public void GF2B_K3_ARAMADA_429_ONBELLEGE_YAZILMAZ()
        {
            var govde = KodSatirlari(MetotGovdesi(Oku("frontend/api-bridge.js"),
                "async function fetchSearch(q)"));

            var limitDali = govde.IndexOf("e.status === 429", StringComparison.Ordinal);
            limitDali.Should().BeGreaterThan(-1, "catch 429'u AYRI dalda tanimali");

            // VAKUM KIRICI: genel hata dali HALA onbellege yaziyor olmali. Bu satir
            // kaybolsaydi asagidaki sira karsilastirmasi anlamsizlasirdi.
            var bosYazma = govde.IndexOf("searchCache = { q: q, items: [] }", StringComparison.Ordinal);
            bosYazma.Should().BeGreaterThan(-1,
                "vakum kirici: genel hata dali onbellege yazmaya devam etmeli");

            // ══ SIRA ONEMLI - ASIL AYIRT EDICI ════════════════════════════════════════
            // 429 kontrolu bos onbellek yaziminden SONRA gelseydi yazma ZATEN olmus olurdu
            // ve dal hicbir sey kurtarmazdi.
            limitDali.Should().BeLessThan(bosYazma,
                "429 kontrolu BOS ONBELLEK YAZIMINDAN ONCE gelmeli");

            govde.Should().Contain("return ARAMA_LIMITLI;",
                "limit dali, bos sonuctan AYIRT EDILEBILIR bir deger dondurmeli");
            Sayim(govde, "searchCache = { q: q, items: [] }").Should().Be(1,
                "onbellege bos sonuc YALNIZ genel hata dalinda yazilmali - ikinci yazma " +
                "limit dalinin da onbellegi kirlettigini gosterir");
        }

        // ══ K3/c - LIMIT EKRANI "SONUC BULUNAMADI" DEMEZ ve YENI SINK ACMAZ ════════════
        [Fact]
        public void GF2B_K3_LIMIT_EKRANI_AYRI_CIZILIR_ve_TEXTCONTENT_KULLANIR()
        {
            var kaynak = Oku("frontend/api-bridge.js");

            kaynak.Should().Contain("if (sonuc === ARAMA_LIMITLI) { aramaLimitEkrani(); return; }",
                "limit yendiginde ozgun cizim CAGRILMAMALI - o, bos onbellegi okuyup " +
                "'Sonuc bulunamadi' yazar ve YANLIS bir olgu iddia eder");

            var ekran = MetotGovdesi(kaynak, "function aramaLimitEkrani()");
            ekran.Should().Contain("ceviri(\"h_rate_limit\"",
                "metin MEVCUT sozluk anahtarindan gelmeli - yeni anahtar EKLENMEDI");

            // GF-2a SINK DISIPLINI: bu yol yeni bir enjeksiyon yuzeyi ACMAMALI.
            Sayim(KodSatirlari(ekran), "innerHTML").Should().Be(0,
                "limit ekrani metni textContent ile yazmali - innerHTML yeni sink acar");
        }

        // ══ K3/d - KUPON: 429 "GECERSIZ" SAYILMAZ [PARA] ═══════════════════════════════
        //
        // OLCULEN KIRIK: `kod >= 400 && kod < 500` dali TUM 4xx'i "sunucu karar verdi,
        // kupon gecersiz" sayiyordu. 429 da bu kovaya dusuyor ve cagiran `kuponuTazele`
        // GECERLI kuponu sepetten KALDIRIYORDU - hiz limitine takilan musteri indirimini
        // KAYBEDIYORDU. GF-3/K9 arama ile kupon dogrulamayi AYNI 20/dk kovasina koydugu
        // icin bu yol siradan bir gezinmede de tetiklenebiliyor.
        [Fact]
        public void GF2B_K3_KUPON_429DA_SEPETTE_KALIR()
        {
            var govde = KodSatirlari(MetotGovdesi(Oku("frontend/api-bridge.js"),
                "window.divisimaKuponDurumu = async function"));

            govde.Should().Contain("kod === 400 || kod === 404 || kod === 422",
                "kupon KALDIRMA yetkisi yalnizca sunucunun KESIN olumsuz kararlarinda olmali");

            // ══ ASIL AYIRT EDICI - GENIS KOVA GERI GELMEMELI ══════════════════════════
            // Bu NEG assert yorumsuz kaynak uzerinde kosar (capa kirlenmesi yapisal cozumu).
            govde.Should().NotContain("kod < 500",
                "genis 4xx kovasi kaldirma yetkisini 429'a da verirdi - GECERLI kupon " +
                "hiz limiti yuzunden sepetten duserdi [PARA]");

            // VAKUM KIRICI: "ulasilamadi" dali gercekten var olmali; yoksa yukaridaki iki
            // assert govde bosalmis halde de yesil kalabilirdi.
            govde.Should().Contain("ulasildi: false",
                "vakum kirici: 429 ve digerleri icin ULASILAMADI dali bulunmali");
        }

        // ══ K4 - RID YALNIZ 409'DA YENILENIR [VERI-BOZAN] ══════════════════════════════
        //
        // OLCULDU (`GuestCheckoutManager.ReplayGuardiAsync`): guard 400'u YALNIZCA o
        // request_id ile bir siparis ZATEN VARKEN ve olcut tutmayinca doner. Istemci
        // 400'de rid'i yenilerse yeni rid icin gecmis kayit BULUNAMAZ, guard bosa duser
        // ve IKINCI SIPARIS + IKINCI REZERVASYON olusur - GF-3/K12'nin replay kapisi
        // fiilen devre disi kalir.
        [Fact]
        public void GF2B_K4_RID_HATA_SONRASI_YALNIZ_409DA_YENILENIR()
        {
            var kaynak = Oku("frontend/api-bridge.js");
            var govde = KodSatirlari(MetotGovdesi(kaynak, "function ridHataSonrasiTazele(e)"));

            govde.Should().Contain("=== 409",
                "yenileme kosulu TAM 409 olmali");

            // ══ ASIL AYIRT EDICI: govdede TEK bir yenileme cagrisi ve TEK bir kosul ═════
            // Ikinci bir dal (orn. 400) eklenirse bu sayimlar buyur ve pin kirilir.
            Sayim(govde, "checkoutIstekIdYenile()").Should().Be(1,
                "yenileme TEK dalda olmali - ikinci dal 400'u de kapsayabilir ve " +
                "replay kapisini bosa dusururdu");
            Sayim(govde, "===").Should().Be(1,
                "TEK durum kodu kosulu bulunmali - ikinci kosul 400/5xx'i de kapsar");

            // Iki cagri yeri: uye `submitOrder` catch'i ve misafir catch'i.
            Sayim(KodSatirlari(kaynak), "ridHataSonrasiTazele(e);").Should().Be(2,
                "hem uye hem misafir hata dali ayni merkezden gecmeli - ikinci kopya ACILMAZ");
        }

        // ══ K4/b - NIYET IMZASI GENIS, SEPET IMZASI DAR ════════════════════════════════
        //
        // rid "AYNI NIYET" demektir; olcut sepet + adres + kupon + bakiye + odeme
        // yontemi. Ama `sepetImzasi` GENISLETILEMEZ: uc tuketicisi daha var ve ucunun de
        // olcutu "SEPET ICERIGI degisti mi"dir (sunucu sepet senkronu, mirror tur imzasi,
        // kupon yeniden dogrulama). Genisletilseydi yalnizca ADRES SECMEK sunucu
        // sepetini yazdirir ve kupon dogrulamayi tetiklerdi - GF-3/K9'un 20/dk kovasina,
        // yani K3'un yeni duzelttigi limitin USTUNE.
        [Fact]
        public void GF2B_K4_NIYET_IMZASI_AYRI_ve_SEPET_IMZASI_DAR_KALIR()
        {
            var kaynak = Oku("frontend/api-bridge.js");

            var niyet = KodSatirlari(MetotGovdesi(kaynak, "function checkoutNiyetImzasi()"));
            niyet.Should().Contain("sepetImzasi()",
                "niyet imzasi sepet imzasini ICERMELI - kopya degil BILESIM");
            foreach (var alan in new[] { "addrId", "useCredit", "method", "kuponImzaAnahtari" })
            {
                niyet.Should().Contain(alan,
                    $"niyet imzasi '{alan}' degisimini yakalamali - govdeye giren her alan " +
                    "rid'in tanimladigi NIYETIN parcasidir");
            }
            niyet.Should().Contain("mgMail",
                "misafir e-postasi imzaya girmeli - K12 replay olcutunun BIRINCI bileseni odur; " +
                "girmezse e-posta degisiminde rid ayni kalir ve musteri 400 dongusunde sikisir");

            // ══ SEPET IMZASI DAR KALMALI - YAN ETKI KAPISI ═════════════════════════════
            var sepet = KodSatirlari(MetotGovdesi(kaynak, "function sepetImzasi()"));
            sepet.Should().Contain("i.product_id",
                "vakum kirici: sepet imzasi hala kalemleri okuyor olmali");
            foreach (var sizinti in new[] { "addrId", "useCredit", "checkoutState" })
            {
                sepet.Should().NotContain(sizinti,
                    $"sepet imzasi '{sizinti}' TASIMAMALI - uc tuketicisinin olcutu " +
                    "'SEPET ICERIGI degisti mi'dir; genisleme onlarda yanlis tetikleme uretir");
            }

            // Iki gonderim yolu da niyete gore tazeler (misafir yolunda EKSIKTI).
            Sayim(KodSatirlari(kaynak), "checkoutIstekIdNiyeteGoreTazele();").Should().Be(2,
                "uye ve misafir gonderimlerinin IKISI de rid'i niyete gore tazelemeli");
        }

        // ══ K2 - SERVICE WORKER KAYDI TEK NOKTADA ══════════════════════════════════════
        //
        // OLCULDU: `index.html` var olmayan bir 'sw.js'i kaydediyordu. `frontend/sw.js`
        // HICBIR commit'te YOK (ilk commit df91863'ten beri olu kod); statik sunucu SPA
        // fallback'i `text/html` dondurdugu icin Chrome kaydi REDDEDIYOR ve
        // `.catch(function(){})` hatayi yutuyordu. Dogru kayit `pwa-register.js`teydi -
        // yani iki kayit vardi, biri hep dusuyordu.
        [Fact]
        public void GF2B_K2_SW_KAYDI_TEK_NOKTADA_ve_DOGRU_YOLDA()
        {
            var index = KodSatirlari(Oku("frontend/index.html"));
            Sayim(index, "serviceWorker.register").Should().Be(0,
                "index.html ARTIK service worker kaydetmemeli - kayit tek noktada, " +
                "pwa-register.js icinde");
            Sayim(index, "'sw.js'").Should().Be(0,
                "var olmayan 'sw.js' yolu kaynakta KALMAMALI");

            var pwa = KodSatirlari(Oku("frontend/pwa-register.js"));
            Sayim(pwa, "serviceWorker.register").Should().Be(1,
                "kayit TEK olmali - ikinci kopya yeniden dogar ve biri hep duser");
            pwa.Should().Contain("register(\"/service-worker.js\")",
                "kayit GERCEKTEN var olan dosyayi, KOK YOLLA gostermeli");

            // VAKUM KIRICI: pinlenen dosya gercekten servis edilen SW olmali.
            Oku("frontend/service-worker.js").Should().NotBeNullOrWhiteSpace(
                "vakum kirici: kaydedilen dosya depoda bulunmali");
        }

        // ══ K2/b - GERI DONUS KAPISI (KILL SWITCH) ═════════════════════════════════════
        //
        // SW bu dalgaya kadar URETIMDE HIC KOSMADI; K2 ile ilk kez gercek kullanicilarda
        // calisacak ve GF-2a/K8'in kararlari da ilk kez uretimde surulecek. Onbellekli
        // bir SW yanlis davranirsa kullanicinin tarayicisinda KALIR ve yeni dagitim ona
        // ULASAMAYABILIR - depoyu geri almak TEK BASINA yetmez. Bu yuzden dagitimla
        // calisan bir geri donus kapisi ZORUNLU.
        [Fact]
        public void GF2B_K2_KILL_SWITCH_VARSAYILAN_KAPALI_ve_UC_OLAYDA_DA_OKUNUR()
        {
            var sw = Oku("frontend/service-worker.js");
            var kod = KodSatirlari(sw);

            kod.Should().Contain("const KAPAT = false;",
                "geri donus bayragi bulunmali ve VARSAYILAN olarak KAPALI olmali - " +
                "yanlislikla true kalirsa SW hicbir kullanicida calismaz");

            // ══ UC OLAY DA AYNI KARARI GORMELI ════════════════════════════════════════
            // Biri atlanirsa YARIM DURUM olusur: orn. `fetch` bayragi gormezse, kendini
            // silmis bir SW hala istekleri yakalamaya devam eder.
            foreach (var olay in new[] { "install", "activate", "fetch" })
            {
                var govde = MetotGovdesi(kod, "addEventListener(\"" + olay + "\"");
                govde.Should().Contain("KAPAT",
                    $"'{olay}' olayi geri donus bayragini OKUMALI - okumayan olay yarim " +
                    "durum uretir");
            }

            // Kapali dalda GERCEKTEN geri donus yapilmali (vakum kirici + cift anlam).
            var activate = MetotGovdesi(kod, "addEventListener(\"activate\"");
            activate.Should().Contain("self.registration.unregister()",
                "kapali dal SW kaydini SILMELI - yalniz onbellek bosaltmak yetmez");
            activate.Should().Contain("keys.map((k) => caches.delete(k))",
                "kapali dal TUM kovalari suzgecsiz bosaltmali - amac geri donus, koruma degil");

            // ══ SURUM BUMPI - K2 BIR KEZLIK KANITI ════════════════════════════════════
            // SW govdesi bu dalgada DEGISTI; CACHE adlari VERSION'dan turedigi icin surum
            // bumplanmazsa `activate` eski kovalari silmez ve degisiklik kullaniciya gec
            // ulasir. Onceki dalganin degeri adiyla anilir ki bump ATLANAMASIN.
            kod.Should().NotContain("VERSION = \"2026-09-03-gf2a\"",
                "SW govdesi degistiginde VERSION da bumplanmali - GF-2a degeri kalmamali");
            kod.Should().Contain("const CACHE = \"divisima-shell-\" + VERSION;",
                "kabuk kovasi surumden turemeli");
            kod.Should().Contain("const API_CACHE = \"divisima-api-\" + VERSION;",
                "API kovasi surumden turemeli - GF-2a/K8'in iki kova karari korunuyor");
        }

        // ══ K5-lite - SATIR ICI OLAY OZNITELIGI KALMADI ════════════════════════════════
        //
        // Satir ici `onclick`/`oninput`/`onload` oznitelikleri CSP'de `'unsafe-inline'`
        // (admin) ve `'unsafe-hashes'` (vitrin) kaynaklarini ZORUNLU kiliyordu. Ikisi de
        // kaldirildi; handler'lar `data-act` ozniteligine ve delege dinleyicilere tasindi.
        //
        // ESLESTIRME CAPASI SINANDI (MK-7): `on[a-z]+=` TEK BASINA YANLIS - `content="`
        // icindeki `ontent=` ve `data-contrast="` icindeki `ontrast=` de eslesiyor
        // (olculdu: POZ 3/2, NEG 1/0). Bu yuzden capa BOSLUK SINIRI tasir.
        [Theory]
        [InlineData("frontend/index.html")]
        [InlineData("frontend/admin.html")]
        [InlineData("frontend/admin.js")]
        [InlineData("frontend/api-bridge.js")]
        public void GF2B_K5_HICBIR_YUZEYDE_SATIR_ICI_OLAY_OZNITELIGI_YOK(string yol)
        {
            var kod = KodSatirlari(Oku(yol));
            var esler = System.Text.RegularExpressions.Regex
                .Matches(kod, @"\son[a-z]+\s*=")
                .Select(m => m.Value.Trim())
                .ToList();

            esler.Should().BeEmpty(
                $"{yol}: satir ici olay ozniteligi KALMAMALI - her biri CSP'de " +
                $"'unsafe-inline'/'unsafe-hashes' gerektirir. Bulunanlar: {string.Join(", ", esler)}");
        }

        // VAKUM KIRICI - YUKARIDAKI TARAMA GERCEKTEN CALISIYOR MU?
        // Yukaridaki dort assert "hicbir sey bulunamadi" diyerek yesil kaliyor. Tarama
        // BOZUK olsaydi (or. regex hicbir seyi eslemeseydi) yine yesil kalirdi. Bu test,
        // AYNI ifadeyi bilinen-POZITIF bir girdide kosarak taramanin sagligini kanitlar.
        [Fact]
        public void GF2B_K5_OLAY_OZNITELIGI_TARAMASI_BILINEN_POZITIFI_YAKALAR()
        {
            const string poz = "<button onclick=\"f()\">x</button>\n<img onerror=\"g()\">";
            const string neg = "<div class=\"normal\" data-contrast=\"high\">y</div>\n"
                             + "<meta content=\"z\"><span data-on=\"k\">q</span>";

            System.Text.RegularExpressions.Regex.Matches(poz, @"\son[a-z]+\s*=").Count
                .Should().Be(2, "bilinen-POZITIF girdide IKI olay ozniteligi bulunmali");
            System.Text.RegularExpressions.Regex.Matches(neg, @"\son[a-z]+\s*=").Count
                .Should().Be(0, "bilinen-NEGATIF girdide HICBIRI eslesmemeli - " +
                    "'content=' icindeki 'ontent=' ve 'data-contrast=' icindeki 'ontrast=' " +
                    "sinir kosulu olmadan YANLIS eslesir (bu turda olculdu)");
        }

        // ══ K5-lite/b - CSP KAYNAKLARI DARALDI ════════════════════════════════════════
        [Fact]
        public void GF2B_K5_ADMIN_CSP_UNSAFE_INLINE_SCRIPT_TASIMAZ()
        {
            var meta = CspMetaIcerigi(Oku("frontend/admin.html"));
            var scriptSrc = Direktif(meta, "script-src");

            scriptSrc.Should().NotContain("'unsafe-inline'",
                "panelin TUM satir ici script'leri disari alindi - kaynak KALKMALI");
            // VAKUM KIRICI + CIFT ANLAM: direktif GERCEKTEN okunmus olmali ve panelin
            // mesru uzak bagimliligi (Chart.js) DURMALI.
            scriptSrc.Should().Contain("https://cdn.jsdelivr.net",
                "vakum kirici: script-src okunmus olmali ve Chart.js kaynagi durmali");

            // STIL tarafi BILINCLI olarak dokunulmadi: panelde `style="..."` oznitelikleri
            // var ve onlar CSS'tir - script yuzeyi degil.
            Direktif(meta, "style-src").Should().Contain("'unsafe-inline'",
                "stil tarafi bu dalgada KAPSAM DISI - kaldirilmasi ayri bir istir");

            // OLU DIREKTIF KALKTI: `frame-ancestors` meta CSP'de SPEC GEREGI yok sayilir.
            // Koruma nginx basligindan geliyor (GuvenlikFix3SozlesmeTests o basligi pinliyor).
            meta.Should().NotContain("frame-ancestors",
                "meta'daki frame-ancestors OLU METINDIR - koruma HTTP basligindan gelir");
        }

        [Fact]
        public void GF2B_K5_VITRIN_CSP_UNSAFE_HASHES_TASIMAZ_ve_3DS_FORM_ACTION_ILE_YURUR()
        {
            var meta = CspMetaIcerigi(Oku("frontend/index.html"));

            Direktif(meta, "script-src").Should().NotContain("'unsafe-hashes'",
                "satir ici olay oznitelikleri kalktigi icin bu kaynak da KALKMALI");

            // ══ frame-src EKLENMEDI - OLCULEN GEREKCE ═════════════════════════════════
            // 3DS akisi bir IFRAME degil, ust duzey FORM POST'udur: `docs/muhur/
            // 01-oturum-devri.md:503` 3DS adimini `form-action` uzerinden kaydediyor ve
            // AYNI muhurde 3DS uctan uca suruldu (#30 dustu, #31 basarili) - `frame-src`
            // CSP'de HIC YOKKEN. Yani kanit "yok" degil, OLUMLU yonde: akis frame-src
            // olmadan calisiyor. Bu pin o mekanizmayi korur.
            var formAction = Direktif(meta, "form-action");
            formAction.Should().Contain("iyzipay.com",
                "3DS adimi form POST ile yurudugu icin saglayici host'u form-action'da olmali");
            meta.Should().NotContain("frame-src",
                "3DS iframe DEGIL - kanitsiz bir frame-src eklemek yuzeyi GEREKSIZ genisletir");

            // Merkezin adiyla istedigi iki kaynak (GF-2a data:image karari ile tutarli).
            Direktif(meta, "font-src").Should().Contain("https://fonts.gstatic.com",
                "Google Fonts dosyalari font-src'de acik olmali");
            Direktif(meta, "img-src").Should().Contain("data:",
                "GF-2a/K3 data:image gorsellerine izin veriyor - CSP onunla tutarli kalmali");
        }

        // ══ K5-lite/c - DELEGASYON BEYAZ LISTE, `window[...]` DEGIL ════════════════════
        //
        // `data-act` degerini dogrudan fonksiyon adina cevirmek (`window[el.dataset.act]()`)
        // DOM'a oznitelik yazabilen bir saldirgana KEYFI global fonksiyon cagirma yetkisi
        // verirdi - yani `'unsafe-inline'`i kaldirmakla kazanilan sey geri verilirdi.
        [Fact]
        public void GF2B_K5_PANEL_DELEGASYONU_BEYAZ_LISTE_KULLANIR()
        {
            var admin = KodSatirlari(Oku("frontend/admin.js"));

            admin.Should().Contain("const PANEL_EYLEM = {",
                "eylemler BEYAZ LISTE tablosunda olmali");
            admin.Should().Contain("hasOwnProperty.call(tablo, el.dataset.act)",
                "arama prototip zincirine DUSMEMELI - 'toString' gibi bir deger eylem sanilmamali");
            admin.Should().NotContain("window[el.dataset",
                "eylem adi dogrudan global cozume CEVRILMEMELI - keyfi fonksiyon cagrisi acardi");

            // Uc olay turu de ayri tabloyla baglanmali: tek tablo olsaydi bir `data-act`
            // yanlis olay turunden de tetiklenebilirdi (input'a tiklamak "Sil" calistirir).
            foreach (var t in new[] { "\"click\", PANEL_EYLEM", "\"input\", PANEL_GIRDI", "\"change\", PANEL_DEGISIM" })
                admin.Should().Contain("panelOlayBagla(" + t + ")",
                    "her olay turu KENDI tablosuyla baglanmali");
        }

        // CSP meta etiketinin `content` degerini cikarir.
        private static string CspMetaIcerigi(string html)
        {
            var m = System.Text.RegularExpressions.Regex.Match(html,
                "<meta[^>]*http-equiv=\"Content-Security-Policy\"[^>]*content=\"([^\"]*)\"");
            m.Success.Should().BeTrue("CSP meta etiketi bulunmali");
            return m.Groups[1].Value;
        }

        // CSP icinden tek bir direktifin degerini cikarir (adi dahil).
        private static string Direktif(string csp, string ad)
        {
            var parca = csp.Split(';')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith(ad + " ", StringComparison.Ordinal) || p == ad);
            parca.Should().NotBeNull($"CSP'de '{ad}' direktifi bulunmali");
            return parca!;
        }
    }
}
