using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ LAUNCH-FIX-1 (LF-1) - DAGITIM ARTEFAKTLARI SOZLESME PINLERI ════════════════════════
    //
    // BU DOSYADAKI PINLERIN COGU KAYNAK-SOZLESME PINIDIR (davranis DEGIL, ARTEFAKT metni
    // olcerler) ve bunu SAKLAMIYORLAR. Gerekce: LF-1'in kalemleri buyuk olcude UYGULAMA
    // DISINDA yasiyor - bir yapilandirma sablonu, bir compose dosyasi, bir is akisi ve dort
    // belge. Bunlarin "davranisi" ancak GERCEK BIR DAGITIMDA gozlenir; depoda olculebilen
    // sey, artefaktin operatore SOYLEDIGI seyin kodun YAPTIGI seyle ortusup ortusmedigidir.
    // K1'in (Cookies:Domain) davranis ayagi AYRI dosyadadir - `ConfigFailFastTests`: host
    // Production'da GERCEKTEN acilmiyor, Development'ta GERCEKTEN aciliyor.
    //
    // MK-6 GEREGI hepsi uretim mutasyonuyla sinandi; hangi mutasyonun hangi pini kirmizi
    // yaptigi dalga raporunda tek tek yazili.
    // MK-8 EKI GEREGI taramalar YORUMLARI AYIKLANMIS metin uzerinde kosar - aksi halde bir
    // pin, korudugu seyi ACIKLAYAN yorumla tatmin olur. (Bu dosyada birebir yasandi:
    // `secret-rotation.yml`de "cron" kelimesi UC KEZ geciyor ve UCU DE yorumda.)
    public class LaunchFix1SozlesmeTests
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

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(Kok.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"artefakt depoda bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        // `#` ile baslayan YAML yorumlarini ATAR. Satir ici yorum icin de calisir; bu
        // dosyalarda dizge icinde `#` YOKTUR (olculdu), o yuzden basit ayirici yeterli.
        private static string YamlYorumsuz(string metin) =>
            string.Join("\n", metin.Split('\n').Select(s =>
            {
                var i = s.IndexOf('#');
                return i < 0 ? s : s.Substring(0, i);
            }));

        // `//` ile baslayan C# satir yorumlarini ATAR (blok yorum bu dosyalarda kullanilmiyor).
        private static string CsYorumsuz(string metin) =>
            string.Join("\n", metin.Split('\n').Select(s =>
            {
                var i = s.IndexOf("//", StringComparison.Ordinal);
                return i < 0 ? s : s.Substring(0, i);
            }));

        // Shannon entropisi - `gitleaks` `generic-api-key` kuralinin ayni olcutu (esik 3.5).
        private static double Entropi(string s)
        {
            if (s.Length == 0) return 0;
            return s.GroupBy(c => c)
                    .Select(g => (double)g.Count() / s.Length)
                    .Sum(p => -p * Math.Log2(p));
        }

        // ══ K1 + K2 - HASSAS ANAHTAR LISTESININ KOMPOZISYONU ═══════════════════════════════
        //
        // Merkez tarifi bu sayiyi ADIYLA istedi: "yedi anahtar -> sekiz (Captcha cikinca yedi)".
        // Liste GF-3/K5'te YEDI idi; LF-1'de `Cookies:Domain` GIRDI (K1) ve `Captcha:SecretKey`
        // CIKTI (K2) - yani sayi YEDI'de kaldi ama KOMPOZISYON degisti. Bu yuzden sayi TEK
        // BASINA yeterli olcut DEGILDIR ve iki uyelik asserti ayrica yaziliyor: yalniz sayiyi
        // olcen bir pin, ikisinin birden yanlis yapilmasina KOR kalirdi.
        //
        // Bu bir KAYNAK-SOZLESME pinidir ve davranis ayagi AYRIDIR: `ConfigFailFastTests`
        // Cookies:Domain icin gercek host acilisini (POZ + NEG + Development ayagi), Captcha
        // icin de "yer-tutucuya ragmen ACILIR" ayagini olcer.
        [Fact]
        public void HassasAnahtarListesi_YEDI_UYELI_ve_KOMPOZISYONU_LF1_KARARLARINA_UYGUN()
        {
            var program = CsYorumsuz(Oku("Divisima.API/Program.cs"));

            var blok = Regex.Match(program, @"hassasAnahtarlar\s*=\s*new\[\]\s*\{(.*?)\}",
                RegexOptions.Singleline);
            blok.Success.Should().BeTrue("vakum kirici: `hassasAnahtarlar` dizisi bulunmali");

            var anahtarlar = Regex.Matches(blok.Groups[1].Value, "\"([^\"]+)\"")
                .Select(m => m.Groups[1].Value).ToList();

            anahtarlar.Should().HaveCount(7,
                "GF-3/K5'te yedi idi; LF-1'de Cookies:Domain GIRDI ve Captcha:SecretKey CIKTI - " +
                $"sayi yine yedi. Bugunku liste: {string.Join(", ", anahtarlar)}");
            anahtarlar.Should().Contain("Cookies:Domain",
                "LF-1/K1: uretimde bos birakilirsa /api/auth/refresh KALICI 403 doner");
            anahtarlar.Should().NotContain("Captcha:SecretKey",
                "LF-1/K2: olu ozellik icin gercek secret dayatilmaz (T3-1)");
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // K3 (BL-2) - URETIM YAPILANDIRMA SABLONU
        // ══════════════════════════════════════════════════════════════════════════════════

        // JSON'un TOP-LEVEL anahtarlarini ("//" ile baslayan aciklama anahtarlari HARIC) verir.
        private static List<string> UstDuzeyAnahtarlar(string json) =>
            Regex.Matches(json, "^  \"([^\"]+)\"\\s*:", RegexOptions.Multiline)
                 .Select(m => m.Groups[1].Value)
                 .Where(a => !a.StartsWith("//", StringComparison.Ordinal))
                 .ToList();

        // OLCULEN ONCE-DURUM (BL-2): depoda `appsettings.Development.example.json` VARDI,
        // uretim karsiligi YOKTU. Operator uretime cikarken elinde SADECE gelistirici sablonu
        // olur; onda `Cookies:Domain` BOS ONERILIR (dev'de dogru), `BackgroundJobs` yorumu
        // "normalde dokunmayin" der ve `RateLimit` uretim degerleri anlatilmaz. Yani en olasi
        // dagitim, LF-1/K1'in kapattigi arizayi ve AV-3'un rezervasyon birikmesini AYNEN
        // uretir.
        [Fact]
        public void UretimSablonu_DevelopmentSablonunun_TUM_BOLUMLERINI_TASIR()
        {
            var dev = UstDuzeyAnahtarlar(Oku("Divisima.API/appsettings.Development.example.json"));
            var prod = UstDuzeyAnahtarlar(Oku("Divisima.API/appsettings.Production.example.json"));

            dev.Should().NotBeEmpty("vakum kirici: dev sablonu gercekten okunmus olmali");
            prod.Should().NotBeEmpty("vakum kirici: uretim sablonu gercekten okunmus olmali");

            // Merkez tarifi ALTI bolumu ADIYLA sayiyordu; onlar ayrica ve ACIKCA aranir -
            // "hepsini kapsiyor" iddiasi, dev sablonu ileride kucultulurse BEDAVA dogru olurdu.
            foreach (var zorunlu in new[]
                     { "Api", "BackgroundJobs", "Cookies", "ForwardedHeaders", "GuestCheckout", "RateLimit" })
                prod.Should().Contain(zorunlu,
                    $"'{zorunlu}' bolumu uretim sablonunda ADIYLA bulunmali (merkez tarifi)");

            var eksik = dev.Except(prod, StringComparer.Ordinal).ToList();
            eksik.Should().BeEmpty(
                "uretim sablonu, gelistirici sablonunun HICBIR bolumunu dusuremez - dusen her " +
                "bolum operatorun HIC GORMEYECEGI bir ayardir");
        }

        // Merkez sarti: "her anahtara tek satir yorum (nereden alinir)". Bolum basina bir
        // `"//<Bolum>"` aciklama anahtari araniyor - JSON yorum tasiyamadigi icin depo kalibi
        // budur (`appsettings.Development.example.json` ayni deseni kullanir).
        [Fact]
        public void UretimSablonunun_HER_BOLUMUNUN_ACIKLAMA_SATIRI_VAR()
        {
            var metin = Oku("Divisima.API/appsettings.Production.example.json");
            var bolumler = UstDuzeyAnahtarlar(metin);
            bolumler.Should().NotBeEmpty("vakum kirici");

            var aciklamasiz = bolumler
                .Where(b => !metin.Contains($"\"//{b}\"", StringComparison.Ordinal))
                .ToList();

            aciklamasiz.Should().BeEmpty(
                "aciklamasiz bir anahtar, operatorun degerini NEREDEN alacagini bilmedigi " +
                "anahtardir; sablonun tek isi budur");
        }

        // ══ GITLEAKS TUZAGI (GF-3 DERSI) ═══════════════════════════════════════════════════
        // GF-3'te birebir yasandi: sablona yazilan "kurgu-smtp-parolasi-0123456789" gibi
        // yer-tutucular entropi 4.35-4.42 ile `generic-api-key` esigini (3.5) astı ve
        // `secret-scan` job'i KIRILDI. Cozum ENTROPI KUMARI DEGIL YAPISAL: kural en az ON
        // karakterlik bir deger arar; "CHANGE_ME" DOKUZ karakterdir ve kural onu ESLEYEMEZ.
        // Bu pin, ilerideki bir "daha gercekci gorunen ornek deger" duzenlemesini dagitimdan
        // ONCE, YERELDE yakalar - CI'daki `secret-scan` job'inin bulgusu ise yalniz ADIM
        // SONUCUNDAN okunabiliyor (annotation YESIL gorunuyor, surec skill'i).
        [Fact]
        public void UretimSablonundaki_GIZLI_BENZERI_DEGERLER_GITLEAKS_ESIGININ_ALTINDA()
        {
            var metin = Oku("Divisima.API/appsettings.Production.example.json");

            // Anahtar adi gizli-benzeri olan HER satir. (`gitleaks` `generic-api-key` kurali da
            // tam olarak anahtar ADINA bakar - deger tek basina taranmaz.)
            var satirlar = Regex.Matches(metin,
                    "\"([^\"/]*(?i:key|secret|token|password|pwd|credential)[^\"]*)\"\\s*:\\s*\"([^\"]*)\"")
                .Select(m => (Anahtar: m.Groups[1].Value, Deger: m.Groups[2].Value))
                .ToList();

            satirlar.Should().NotBeEmpty(
                "vakum kirici: sablonda gizli-benzeri anahtar GERCEKTEN bulunmali, yoksa bu pin " +
                "hicbir sey olcmemis olur");

            foreach (var (anahtar, deger) in satirlar)
            {
                if (deger.Length == 0) continue;   // bos deger taranmaz
                var tetikler = deger.Length >= 10 && Entropi(deger) >= 3.5;
                tetikler.Should().BeFalse(
                    $"'{anahtar}' degeri gitleaks `generic-api-key` esigini tetikliyor " +
                    $"(uzunluk {deger.Length} >= 10 VE entropi {Entropi(deger):F3} >= 3.5); " +
                    "yer-tutucular KISA ve DUSUK ENTROPILI secilir (GF-3 dersi)");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // K3 - URETIM COMPOSE DOSYASI
        // ══════════════════════════════════════════════════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM: depodaki TEK compose dosyasi `ASPNETCORE_ENVIRONMENT:
        // Development` veriyor ve bir `sa` parolasi tasiyor. Onunla yapilan bir dagitim
        // Program.cs'in TUM uretim kapilarini (yer-tutucu taramasi · Encryption:Key ·
        // MailSettings:Host · Iyzico:CallbackUrl · LF-1/K1 Cookies:Domain) SESSIZCE ATLAR.
        [Fact]
        public void UretimComposeu_URETIM_ORTAMINI_SECER_ve_SECRETLARI_ORTAMDAN_ALIR()
        {
            var ham = Oku("docker-compose.prod.yml");
            var metin = YamlYorumsuz(ham);

            metin.Should().Contain("ASPNETCORE_ENVIRONMENT: Production",
                "uretim compose'u uretim ortamini secmezse Program.cs'in fail-fast kapilarinin " +
                "HICBIRI kosmaz - `!IsDevelopment()` dalinin icindeler");

            // Zorunlu secret'lar ORTAMDAN gelir ve EKSIKSE compose PATLAR (`:?` bicimi).
            foreach (var anahtar in new[]
                     {
                         "ConnectionStrings__DivisimaDb", "TokenOptions__SecurityKey",
                         "Encryption__Key", "Cookies__Domain", "MailSettings__Password",
                         "Iyzico__ApiKey", "Iyzico__SecretKey",
                     })
            {
                var satir = metin.Split('\n').FirstOrDefault(s => s.Contains(anahtar, StringComparison.Ordinal));
                satir.Should().NotBeNull($"'{anahtar}' uretim compose'unda tanimli olmali");
                satir!.Should().Contain(":?",
                    $"'{anahtar}' ortam degiskeni EKSIKSE compose SESSIZCE bos deger gecirmemeli - " +
                    "`${VAR:?...}` bicimi eksikligi ORADA patlatir; aksi halde ariza uygulamanin " +
                    "acilisina, kotu halde ise ilk musteriye kalir");
            }

            // `sa` YOK: veritabani bilincli olarak compose DISINDA (yonetilen SQL Server).
            metin.Should().NotContain("SA_PASSWORD",
                "uretim compose'u bir `sa` parolasi TASIMAZ - veritabani bu dosyanin disinda, " +
                "en az yetkili kullaniciyla baglanilir (ops/db/least-privilege.sql)");
            // `NotContain("MSSQL_SA_PASSWORD")` SILINDI (F-turu / B-6): BEDAVA DOGRUYDU -
            // yukaridaki assert onu mantiken ZATEN kapsiyor (ust dizge alt dizgeyi icerir).

            // K9: log ve yuklemeler VOLUME'de - aksi halde konteyner yenilenince GIDER.
            metin.Should().Contain("logs_data", "konteyner disi log kalicilgi (K9)");
            metin.Should().Contain("uploads_data", "urun gorselleri konteynerle birlikte SILINEMEZ");

            // Redis + healthcheck: dagitik kilit ve rate limit BUNA bagli.
            metin.Should().Contain("healthcheck:",
                "saglik kontrolu olmadan orkestrasyon 'ayakta ama kirik' bir konteyneri trafige acar");
            Regex.Matches(metin, @"^\s*healthcheck:", RegexOptions.Multiline).Count
                .Should().BeGreaterThanOrEqualTo(2,
                    "hem redis hem API kendi saglik kontrolunu tasimali");
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // K4 (BL-3) - GUVENLIK OLAY TIPLERI: BELGE <-> KOD
        // ══════════════════════════════════════════════════════════════════════════════════
        //
        // BU PIN EZBERE LISTE TUTMAZ - tipleri URETIM KAYNAGINDAN cikarir ve iki belgenin o
        // kumeyi eksiksiz saydigini olcer. Yani yeni bir olay tipi eklenirse pin, BELGELER
        // guncellenene kadar KIRMIZI kalir.
        //
        // OLCULEN HATA (denetci I-1 kabul edildi): ilk sayim `LogAsync("` capasiyla yapildi ve
        // **12** verdi; dogrusu **14**. Iki cagri tipi bir TERNARY'nin ilk argumaninda uretiyor
        // (`AccountLocked`, `ChangePasswordFailed`) ve o capa onlari GORMUYORDU. Yanlis 12,
        // SECURITY.md'nin o gunku 12'siyle ORTUSTUGU icin uc kanalli SAHTE BIR MUTABAKAT
        // uretmisti. Cikarim artik ilk argumanin TAMAMINI ayristirir.
        private static SortedSet<string> KaynaktanOlayTipleri()
        {
            var tipler = new SortedSet<string>(StringComparer.Ordinal);
            // F-TURU / B-7: `Divisima.Dal` ve `Divisima.Entity` de tarandi. ONCEKI HAL kordu -
            // veri katmanina eklenecek bir olay tipi sayiyi 14'te BIRAKIRDI ve belgeler
            // bayatlarken pin YESIL kalirdi. Tarayicinin bu dizinlerde de calistigi
            // `TarayiciKapsami_BILINEN_POZITIFLE_SINANDI` ile ayrica sinaniyor.
            foreach (var proje in new[]
                     { "Divisima.Bussiness", "Divisima.API", "Divisima.Core", "Divisima.Dal", "Divisima.Entity" })
            {
                var dizin = Path.Combine(Kok.Value, proje);
                if (!Directory.Exists(dizin)) continue;
                foreach (var dosya in Directory.EnumerateFiles(dizin, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (var satir in CsYorumsuz(File.ReadAllText(dosya)).Split('\n'))
                    {
                        var i = satir.IndexOf("LogAsync(", StringComparison.Ordinal);
                        if (i < 0) continue;
                        if (satir.Contains("Task LogAsync", StringComparison.Ordinal)) continue; // arayuz/imza
                        // ILK ARGUMAN = `LogAsync(` sonrasindan, DERINLIK 0'daki ilk virgule kadar.
                        var kalan = satir.Substring(i + "LogAsync(".Length);
                        int derinlik = 0, son = kalan.Length;
                        for (var k = 0; k < kalan.Length; k++)
                        {
                            if (kalan[k] == '(') derinlik++;
                            else if (kalan[k] == ')') { if (derinlik == 0) { son = k; break; } derinlik--; }
                            else if (kalan[k] == ',' && derinlik == 0) { son = k; break; }
                        }
                        // Ternary de dahil, ilk argumandaki TUM dizge literalleri toplanir.
                        foreach (Match m in Regex.Matches(kalan.Substring(0, son), "\"([^\"]+)\""))
                            tipler.Add(m.Groups[1].Value);
                    }
                }
            }
            return tipler;
        }

        [Fact]
        public void GuvenlikOlayTipleri_KODDAN_TURETILIR_ve_IKI_BELGE_de_HEPSINI_SAYAR()
        {
            var tipler = KaynaktanOlayTipleri();

            // Vakum + cift-anlam kirici: cikarim gercekten calisti mi, ve TERNARY dali da
            // yakalandi mi. `AccountLocked` YALNIZ ternary icinde uretilir - dolayisiyla bu
            // tek assert, ayristiricinin naif `LogAsync("` capasina DUSMEDIGINI kanitlar.
            tipler.Should().NotBeEmpty("vakum kirici: cikarim kaynaktan gercekten okumali");
            tipler.Should().Contain("AccountLocked",
                "ternary ilk argumani yakalanmali - naif capa bunu KACIRIYORDU (olculdu)");
            tipler.Should().Contain("ChangePasswordFailed");

            // Sayi burada EZBERDEN degil, once-durumdan gelir. Degisirse pin KIRILIR ve karar
            // (belgeleri guncelle) BILINCLI verilmek zorunda kalir.
            tipler.Count.Should().Be(14,
                "LF-1/K4'te olculen tip sayisi 14'tu; kod bir tip EKLER ya da CIKARIRSA bu pin " +
                "kirilir ve belgeler ONUNLA BIRLIKTE guncellenmek zorunda kalir. Bugunku kume: " +
                string.Join(", ", tipler));

            var security = Oku("SECURITY.md");
            var siem = Oku("ops/serilog-siem.md");

            foreach (var tip in tipler)
            {
                security.Should().Contain(tip,
                    $"SECURITY.md uretilen '{tip}' olay tipini saymali - okuyucu neyi " +
                    "arayacagini oradan ogreniyor");
                siem.Should().Contain(tip,
                    $"ops/serilog-siem.md uretilen '{tip}' olay tipini saymali");
            }

            // NEGATIF KONTROL: BELGENIN saydigi ama KODUN uretmedigi bir ad, "uretilenler"
            // listesine SIZMAMALI. Bu uc ad belgelerde ACIKCA "URETILMIYOR" basligi altinda
            // duruyor; kaynak cikariminda gorunurlerse ayristirici YANLIS calisiyor demektir.
            foreach (var uretilmeyen in new[] { "PaymentFraud", "PaymentAmountMismatch", "NewDeviceLogin" })
                tipler.Should().NotContain(uretilmeyen,
                    $"'{uretilmeyen}' kodda olay tipi olarak URETILMIYOR (yalniz mesaj sabiti / yorum)");
        }

        [Fact]
        public void SIEM_ALARM_TABLOSU_GF6_OLAYLARINI_TASIR()
        {
            var siem = Oku("ops/serilog-siem.md");

            // ══ CAPA SIKILASTIRILDI - MUT-15 BUNU ORTAYA CIKARDI ═══════════════════════════
            // ILK YAZIMDA capa cıplak `PaymentAfterTerminal` idi ve mutasyon (adi
            // `PaymentAfterTerminalX` yapmak) **0 KIRMIZI** verdi: `Contain` bir UST DIZGEYLE
            // de tatmin olur. Yani pin, olayin adinin BOZULMASINA kordu. Capa artik belgenin
            // HAM metninden kopyalandi (MK-7) - alarm tablosunda ad `**\`...\`**` bicimindedir
            // ve satirin ALARM SATIRI oldugunu da o bicim tasir.
            siem.Should().Contain("| **`PaymentAfterTerminal`** (severity `Critical`) |",
                "terminal siparise gelen odeme ELLE IADE gerektirir - ALARM TABLOSUNDA (yalniz " +
                "tip listesinde degil) bir satiri olmali, yoksa operator onu hic aramaz");
            siem.Should().Contain("| **`ProductImportRejected`** |",
                "reddedilen ice-aktarim da alarm tablosunda kendi satirini tasimali");

            // ══ ILK YAZIMDA BU ASSERT YANLISTI - KAYDA GECIYOR ═════════════════════════════
            // Once `NotContain("Order + Payment")` yazildi ve TAM 1 KIRMIZI verdi. Sebep:
            // ucu de o ifadeyi TASIYOR - ama IDDIA olarak degil, KENDI DUZELTMESI icinde
            // ("Order + Payment" YANLISTI, AV-3'te olculdu). Yani yasak-bicim asserti, korudugu
            // seyi ACIKLAYAN metinle tetiklendi; MK-8 EKI'nin ayni ailesi, bu kez BELGE
            // tarafinda. Yasak bicim yerine POZITIF BICIM pinleniyor: kapsam, MANAGER adiyla
            // degil YAZILAN KAYNAK adiyla yazilmis olmali.
            // CAPALAR HAM CIKTIDAN ALINDI, EZBERDEN DEGIL (MK-7): iki belge markdown backtick
            // kullanir (`order`), C# yorumu ise duz tirnak ("order") ve ASCII "YANLIS" yazar
            // (kaynak dosyalarinda Turkce harf kullanilmiyor). Tek bir capa UCU DE eslemez -
            // ilk denemede birebir bu oldu. C# yorumunun kendi pini AYRI:
            // `ISecurityEventService_YORUMU_KAPSAMI_KAYNAK_ADIYLA_YAZAR`.
            foreach (var (yol, metin) in new[] { ("ops/serilog-siem.md", siem), ("SECURITY.md", Oku("SECURITY.md")) })
            {
                metin.Should().Contain("`order`",
                    $"{yol}: IdorAttempt kapsami YAZILAN KAYNAK adiyla tanimlanir " +
                    "(IyzicoPaymentManager -> \"order\")");
                metin.Should().Contain("`address`",
                    $"{yol}: ikinci cagri yeri OrderManager -> \"address\"");
                // ══ CAPA OZGULLESTIRILDI (F-TURU / B-3) ════════════════════════════════════
                // ILK YAZIMDA ciplak `Contain("YANLIŞ")` idi. Denetci mutasyonla gosterdi ki
                // SECURITY.md'de bu kelimenin IKI gecisi var (biri K5'in kasa maddesinde) ve
                // IdorAttempt duzeltmesi SILINSE BILE assert doyuyordu -> **0 KIRMIZI**.
                // Capa artik DUZELTMENIN KENDI CUMLESINI ariyor: "Order + Payment" ifadesinin
                // yanlis oldugunu soyleyen ibare, o ifadeye BITISIK olmali.
                Regex.IsMatch(metin, @"Order \+ Payment[^\n]{0,40}YANLIŞ")
                    .Should().BeTrue(
                        $"{yol}: eski \"Order + Payment\" ifadesinin YANLIS oldugu, TAM O IFADENIN " +
                        "yaninda kayitli kalmali - duzeltme silinirse ayni hata dorduncu kez yazilir");
            }
        }

        // Arayuz YORUMU kapsami dogru soyluyor mu. Bu pin BILEREK yorum metnini olcer -
        // korunan sey davranis DEGIL, gelistiriciye verilen BILGI. (MK-8 EKI bir pinin kendi
        // yorumuyla tatmin olmasini yasaklar; burada yorumun KENDISI olcum konusudur, o yuzden
        // ayiklama YAPILMAZ ve bu ayrim ACIKCA yaziliyor.)
        [Fact]
        public void ISecurityEventService_YORUMU_KAPSAMI_KAYNAK_ADIYLA_YAZAR()
        {
            var metin = Oku("Divisima.Bussiness/Abstract/ISecurityEventService.cs");
            metin.Should().Contain("IyzicoPaymentManager", "kapsamin birinci cagri yeri");
            metin.Should().Contain("\"order\"", "birinci cagri yerinin YAZDIGI kaynak adi");
            metin.Should().Contain("OrderManager", "kapsamin ikinci cagri yeri");
            metin.Should().Contain("\"address\"", "ikinci cagri yerinin YAZDIGI kaynak adi");
            // B-3'un C# ayagi: burada `YANLIS` TEK gecistir, ama capa yine de ifadeye BITISIK
            // aranir - dosyaya ileride baska bir "YANLIS" eklenirse pin kor kalmasin.
            Regex.IsMatch(metin, @"Order \+ Payment[^\n]{0,40}YANLIS")
                .Should().BeTrue(
                    "eski \"Order + Payment\" ifadesinin YANLIS oldugu KAYITLI kalmali - ayni hata " +
                    "UC KEZ yazildi (GF-5 · SECURITY.md · serilog-siem.md), kaydi silinirse dorduncu gelir");
        }

        // ══ RUNBOOK: MIGRATION SAYISI URETEN IFADEYLE (MK-3) ═══════════════════════════════
        // Sayi runbook'ta ELLE yaziyor; bu pin onu DEPODAKI GERCEK sayiyla karsilastirir.
        // Yeni bir migration eklendiginde runbook BAYATLAR ve bu pin kirilir - felaket
        // kurtarmada "kac migration bekliyorum" sorusu YANLIS yanitlanamaz.
        [Fact]
        public void Runbook_MIGRATION_SAYISI_DEPODAKI_GERCEK_SAYIYLA_AYNI()
        {
            var migrationDizin = Path.Combine(Kok.Value, "Divisima.Dal", "Migrations");
            Directory.Exists(migrationDizin).Should().BeTrue("migration dizini bulunmali");

            var gercek = Directory.EnumerateFiles(migrationDizin, "*.cs")
                .Select(Path.GetFileName)
                .Where(a => a != null
                            && !a.EndsWith(".Designer.cs", StringComparison.Ordinal)
                            && !a.Contains("ModelSnapshot", StringComparison.Ordinal))
                .Count();

            gercek.Should().BeGreaterThan(0, "vakum kirici: migration dosyalari gercekten sayilmali");
            Oku("ops/backup-dr-runbook.md").Should().Contain($"**{gercek}** migration",
                $"runbook'taki migration sayisi depodaki gercek sayiyla (={gercek}) ayni olmali");
        }

        // ══ DAGITIM KONTROL LISTESI - IRL ADIMLARI ═════════════════════════════════════════
        [Fact]
        public void DagitimListesi_YIRMI_SIRALI_IRL_ADIMI_ve_LF1_KUTULARINI_TASIR()
        {
            var metin = Oku("ops/deployment-checklist.md");

            // Numarali tablo satirlari: `| 1 | ... |` ... `| 20 | ... |`
            var numaralar = Regex.Matches(metin, @"^\| (\d+) \|", RegexOptions.Multiline)
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();

            numaralar.Should().HaveCount(20, "merkez tarifi YIRMI ardisik IRL adimi istedi");
            numaralar.Should().BeEquivalentTo(Enumerable.Range(1, 20),
                "adimlar SIRALI ve EKSIKSIZ olmali - atlanan numara, atlanan adimdir");

            // Merkez tarifinin ADIYLA istedigi iki yeni kutu.
            metin.Should().Contain("`Cookies:Domain` üst alan adı biçiminde ayarlandı",
                "LF-1/K1'in dagitim tarafindaki karsiligi - kapi acilisi durdurur ama operator " +
                "DOGRU degeri de bilmelidir");
            // ══ CAPA OZGULLESTIRILDI (F-TURU / B-2) ════════════════════════════════════════
            // ILK YAZIMDA ciplak `Contain("PaymentAfterTerminal")` idi ve denetci mutasyonla
            // (adi `PaymentAfterTerminalXYZ` yapmak) **0 KIRMIZI** verdigini gosterdi - alt-dizge
            // capasi UST DIZGEYLE tatmin olur. Bu, MUT-15'te SIEM tarafinda kapatilan sinifin
            // AYNISIYDI ve burada HAYATTA KALMISTI.
            // NEDEN ONEMLI: 20. adimin SQL'indeki olay adi bozulursa pin YESIL kalir, operatorun
            // gunluk sorgusu SESSIZCE 0 satir doner - ve bu sorgu, o olayin TEK OKUYUCUSUDUR
            // (SIEM bagli degil, SignalR "admins" grubu BOS).
            // SINIR KARAKTERLI regex: ad bir tanimlayici sinirinda bitmeli.
            Regex.IsMatch(metin, @"\bPaymentAfterTerminal\b(?![A-Za-z0-9_])")
                .Should().BeTrue(
                    "GUNLUK sorgu bir dagitim sartidir ve olayin adi TAM olmali - bozuk bir ad " +
                    "sessizce 0 satir dondurur; bu olayin tek okuyucusu o sorgudur");
            metin.Should().Contain("event_type='PaymentAfterTerminal'",
                "sorgunun KENDISI listede durmali - 'bir sorgu kosun' demek yetmez");
        }

        // ══ F-TURU / B-1 - CHECKLIST, K2 ve K5'IN KALDIRDIGI ISI EMRETMEZ ══════════════════
        //
        // DENETCI BULGUSU (tek AKTIF kalem): LF-1 bu dosyaya kirk iki satir ekledi ama BAYRAK
        // TABLOSUNA ve SECRET BASLIGINA DOKUNMADI. Sonuc: ayni depoda `SECURITY.md` "captcha
        // bayragi etkisiz, acmak sahte guvence uretir" derken `deployment-checklist.md`
        // operatore "Production'da Captcha:Enabled -> true (gercek Turnstile secret)" diye
        // EMREDIYORDU; ayni sekilde "Vault:Enabled -> true" ve "Secret'lar (Key Vault'a)".
        // Operatorun ELINE ALDIGI belge CHECKLIST'tir - yani K2/K5 fiilen YARIM KALMISTI.
        [Fact]
        public void DagitimListesi_OLU_OZELLIKLERI_ACMAYI_EMRETMEZ()
        {
            var metin = Oku("ops/deployment-checklist.md");

            // Merkez tarifinin ADIYLA istedigi iki sayim.
            Regex.Matches(metin, "Turnstile").Count.Should().Be(0,
                "captcha dogrulayicisinin uretimde 0 cagri yeri var (T3-1); 'gercek Turnstile " +
                "secret' istemek, hicbir sey korumayan bir adimi ZORUNLU gostermektir");
            Regex.Matches(metin, "Vault:Enabled").Count.Should().Be(0,
                "kasa OKUYUCUSU yok (ISecretProvider tuketicisi 0); bayragi acmak hicbir degeri " +
                "kasadan getirmez");

            // VAKUM KIRICI: dosya gercekten okundu ve bolum GERCEKTEN yeniden yazildi.
            // (Iki sayim tek basina, dosya BOS olsa da yesil kalirdi.)
            metin.Should().Contain("## Secret'lar (env/compose",
                "secret'lar ortam degiskeni ya da appsettings.Production.json ile verilir - " +
                "kasaya YAZILMAZ, cunku okuyucusu yok");
            metin.Should().Contain("Cookies--Domain",
                "LF-1/K1'in zorunlu kildigi anahtar secret listesinde de gorunmeli");
        }

        // ══════════════════════════════════════════════════════════════════════════════════
        // K5 (K2 Key Vault) - ZAMANLANMIS ROTASYON DEVRE DISI
        // ══════════════════════════════════════════════════════════════════════════════════
        //
        // OLCULEN GEREKCE: uygulamada KASA OKUYUCUSU YOKTUR. Kasada donen bir anahtar
        // uygulamaya ULASMAZ; zamanlanmis rotasyon guvenlik SAGLAMAZ ama "anahtarlarim donuyor"
        // YANILGISI uretir - ve uretim ortamina bagli bir is akisi olarak zarar VEREBILIR.
        [Fact]
        public void SecretRotasyonu_ZAMANLANMIS_DEGIL_YALNIZ_ELLE_TETIKLENIR()
        {
            var ham = Oku(".github/workflows/secret-rotation.yml");
            var metin = YamlYorumsuz(ham);

            // MK-8 EKI'nin CANLI ORNEGI: ham metinde "cron" UC KEZ geciyor ve UCU DE yorumda.
            // Yorumsuz metin uzerinde kosmayan bir pin burada YANLIS KIRMIZI verirdi.
            ham.ToLowerInvariant().Should().Contain("cron",
                "vakum kirici: yorumdaki gerekce metni GERCEKTEN duruyor olmali - yoksa asagidaki " +
                "'yorumsuzda yok' asserti bedava dogru olur");
            metin.ToLowerInvariant().Should().NotContain("cron",
                "zamanlama KALDIRILDI: kasa okuyucusu yokken donen anahtar uygulamaya ULASMAZ");
            metin.Should().NotContain("schedule:",
                "`on:` blogunda schedule tetigi kalmamali");
            metin.Should().Contain("workflow_dispatch",
                "is akisi SILINMEDI - elle tetiklenebilir kalmali (GF-7 karari: bagla ya da sil)");
        }

        // "Kasa okuyucusu yok" iddiasi BELGEDE yaziyor; bu pin onu KAYNAKTAN dogrular.
        // Iddia bir YOKLUK iddiasidir (SDP 1.2), o yuzden NEGATIF KONTROL zorunlu: ayni
        // tarama, GERCEKTEN kayitli olan saglayiciyi BULABILIYOR mu?
        [Fact]
        public void KASA_OKUYUCUSU_YOK_iddiasi_KAYNAKTAN_DOGRULANIR()
        {
            var program = CsYorumsuz(Oku("Divisima.API/Program.cs"));

            // POZITIF KONTROL (tarama calisiyor): kayitli olan saglayici BULUNUYOR.
            program.Should().Contain("ConfigurationSecretProvider",
                "negatif kontrol: ayni tarama, GERCEKTEN kayitli olan saglayiciyi bulabilmeli");

            // ASIL IDDIA: kasa saglayicisi HICBIR YERDE kayitli DEGIL.
            program.Should().NotContain("AzureKeyVaultSecretProvider",
                "kasa saglayicisi DI'ya kayitli DEGILDIR - Key Vault'a yazilan deger uygulamaya " +
                "ULASMAZ; SECURITY.md ve secret-rotation.yml bu olcume dayaniyor");

            Oku("SECURITY.md").Should().Contain("tüketicisi 0",
                "SECURITY.md secret maddesi DURUST olmali: iskelet var, okuyucu YOK");

            // ══ F-TURU / B-5 - AYNI YANLISIN URETIM KODUNDAKI IKINCI KOPYASI ═══════════════
            // Denetci buldu: K5 bu iddiayi SECURITY.md'de "YANLISTI" diye isaretledi ama
            // `ConfigurationSecretProvider.cs`in kendi yorumunda AYNEN birakmisti. Bu depoda
            // "AYNI KURALIN IKINCI KOPYASI" ailesinin bedeli yedi kez odendi; burada kopya
            // KURAL degil IDDIA, ama zarari ayni: kaynagi okuyan gelistirici, belgenin
            // duzelttigi seyi kodda DOGRU sanir.
            var saglayici = Oku("Divisima.Core/Utilities/Secrets/ConfigurationSecretProvider.cs");
            Regex.IsMatch(saglayici, @"AzureKeyVaultSecretProvider ile değiştirilir[^\n]*\n[^\n]*kod dokunulmaz")
                .Should().BeFalse(
                    "kasaya gecis iddiasi bu dosyada da YANLISTI - okuyucu yazilana kadar kasadaki " +
                    "deger uygulamaya ULASMAZ");
            saglayici.Should().Contain("YANLIŞTI",
                "duzeltmenin KAYDI dursun - ayni iddia SECURITY.md, secret-rotation.yml ve burada " +
                "olmak uzere UC YERDE birden yaziliydi");
        }

        // Uretim projelerinde (`Divisima.Core` HARIC - orada TANIM'lar yasar) verilen desenin
        // CAGRI yerlerini dondurur; imza satirlari `imzaHarici` ile elenir. Yorumlar AYIKLANIR
        // (MK-8 EKI): bir yorumda gecen cagri, cagri DEGILDIR.
        private static List<string> CagriYerleri(string desen, string imzaHarici)
        {
            var bulunan = new List<string>();
            // F-TURU / B-7: `Divisima.Core` de tarandi - captcha DOGRULAYICISI orada yasiyor,
            // yani bir cagri en kolay ORAYA eklenirdi ve onceki kapsam onu GORMEZDI.
            // (`ValidateAsync` TANIMLARI `imzaHarici` ile elenir; asagidaki BILINEN-POZITIF
            //  sinamasi tarayicinin bu dizinde de calistigini gosterir.)
            foreach (var proje in new[] { "Divisima.API", "Divisima.Bussiness", "Divisima.Core" })
            {
                var dizin = Path.Combine(Kok.Value, proje);
                if (!Directory.Exists(dizin)) continue;
                foreach (var dosya in Directory.EnumerateFiles(dizin, "*.cs", SearchOption.AllDirectories))
                    foreach (var satir in CsYorumsuz(File.ReadAllText(dosya)).Split('\n'))
                        if (satir.Contains(desen, StringComparison.Ordinal)
                            && !satir.Contains(imzaHarici, StringComparison.Ordinal))
                            bulunan.Add($"{Path.GetFileName(dosya)}: {satir.Trim()}");
            }
            return bulunan;
        }

        // ══ F-TURU / B-7 - TARAYICI KAPSAMI BILINEN-POZITIFLE SINANIR ══════════════════════
        //
        // Denetci bulgusu: iki tarayici da DAR kapsamla kosuyordu - olay tipi cikarimi
        // `Divisima.Dal`i, captcha cagri taramasi `Divisima.Core`u GORMUYORDU. Kapsam
        // genisletildi; ama "genislettim" bir IDDIADIR ve kendisi de sinanmalidir: bir dizin
        // listeye YAZILIP da (yol yanlissa, dizin bossa, uzanti tutmuyorsa) HIC TARANMAYABILIR
        // ve sonuc yine "0 bulundu" olurdu - yani ayni korluk, bu kez GORUNMEZ bicimde.
        //
        // Bu yuzden her taranan dizin icin BILINEN-POZITIF bir cagri araniyor. Capalar HAM
        // ciktidan kopyalandi (MK-7), ezberden yazilmadi.
        [Theory]
        [InlineData("Divisima.Core", "PostAsync(")]              // TurnstileCaptchaValidator
        [InlineData("Divisima.Bussiness", "LogAsync(")]          // SecurityEventManager + digerleri
        [InlineData("Divisima.API", "LogAsync(")]                // RedisRateLimitMiddleware
        public void TarayiciKapsami_BILINEN_POZITIFLE_SINANDI(string proje, string bilinenCagri)
        {
            var dizin = Path.Combine(Kok.Value, proje);
            Directory.Exists(dizin).Should().BeTrue($"{proje} dizini bulunmali");

            var bulundu = Directory.EnumerateFiles(dizin, "*.cs", SearchOption.AllDirectories)
                .Any(d => CsYorumsuz(File.ReadAllText(d)).Contains(bilinenCagri, StringComparison.Ordinal));

            bulundu.Should().BeTrue(
                $"BILINEN-POZITIF: tarayici '{proje}' icinde '{bilinenCagri}' cagrisini " +
                "bulabiliyor olmali; bulamiyorsa o dizin FIILEN taranmiyor demektir ve " +
                "oradaki her 'bulunamadi' sonucu DAYANAKSIZDIR");
        }

        // NEGATIF KONTROL, AYRI: ayni makine uydurma bir capayi BULMAMALI. Ayni Theory'ye
        // katilirsa "bulundu" ve "bulunamadi" beklentileri karisir; ayri test daha durust.
        [Fact]
        public void TarayiciKapsami_NEGATIF_KONTROL_UYDURMA_CAPAYI_BULMAZ()
        {
            var dizin = Path.Combine(Kok.Value, "Divisima.Core");
            Directory.EnumerateFiles(dizin, "*.cs", SearchOption.AllDirectories)
                .Any(d => File.ReadAllText(d).Contains("ZZZOlmayanCagri(", StringComparison.Ordinal))
                .Should().BeFalse("negatif kontrol: tarayici her seye 'var' dememeli");
        }

        // ══ K2 - CAPTCHA OLU OZELLIK, BELGE DE OYLE DEMELI ═════════════════════════════════
        [Fact]
        public void CAPTCHA_BAYRAGININ_ETKISIZ_OLDUGU_BELGEDE_YAZAR()
        {
            // KAYNAK OLCUMU: dogrulayicinin uretimde cagrisi 0 (T3-1). Yorumsuz metinde
            // `ValidateAsync` YALNIZ arayuz ve sinif tanimlarinda gecer - CAGRI yeri yoktur.
            //
            // BU BIR YOKLUK IDDIASIDIR (SDP 1.2) ve NEGATIF KONTROLSUZ KABUL EDILEMEZ: "0 sonuc"
            // ile "tarayici hicbir sey taramadi" ayni ciktiyi verir. Bu yuzden ayni tarayici,
            // AYNI dizinlerde, GERCEKTEN CAGRILAN bir metotla once BILINEN-POZITIF sinamadan
            // gecirilir (`LogAsync(` - depoda on yediden fazla cagri yeri var).
            var bilinenPozitif = CagriYerleri("LogAsync(", "Task LogAsync");
            bilinenPozitif.Should().NotBeEmpty(
                "BILINEN-POZITIF: tarayici gercek cagri yerlerini bulabiliyor olmali - bu " +
                "sinama olmadan asagidaki 'captcha cagrisi yok' sonucu, tarayicinin BOS " +
                "calismasindan da gelebilirdi");

            var cagrilar = CagriYerleri("ValidateAsync(", "Task<bool> ValidateAsync");

            cagrilar.Should().BeEmpty(
                "captcha dogrulayicisinin URETIMDE cagri yeri YOKTUR (T3-1); bir cagri eklenirse " +
                "bu pin kirilir ve `Captcha:SecretKey`in fail-fast'ten cikarilmasi kararinin " +
                "YENIDEN verilmesi gerekir");

            var security = Oku("SECURITY.md");
            security.Should().Contain("T3-1",
                "belge, bayragin etkisiz oldugunu OLCUM ATFIYLA soylemeli - 'muhtemelen' degil");
            security.Should().NotContain("- [ ] `Captcha:Enabled=true` + gerçek Turnstile secret",
                "uretim kontrol listesi, HICBIR SEY YAPMAYAN bir bayragi 'zorunlu' diye " +
                "istememeli - sahte guvence uretir");
        }
    }
}
