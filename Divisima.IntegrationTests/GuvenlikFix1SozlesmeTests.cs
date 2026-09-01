using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GF-1 SOZLESME PINLERI (K4 negatif kontrolu + K5) ═══════════════════════════════════
    //
    // BU DOSYADAKI PINLERIN COGU KAYNAK-SOZLESMESI PINIDIR ve bu ACIKCA yazilir (SDP:
    // "kaynak-sozlesmesi pinleri isaretlenir ve davranis kanitinin NEREDE oldugu soylenir").
    //   - K4'un DAVRANIS kaniti `AuthorizationIdorTests.K4_UC_SAHIPLIK_NOKTASI_404_...`tedir;
    //     buradaki pin yalniz "geri kalan 403'ler DEGISMEDI" negatif kontroludur.
    //   - K5'in (b)/(c) yuzeyleri icin CI'da JS/DOM ya da SignalR/Hangfire kosucusu YOKTUR
    //     (kayitli rig kor noktasi); davranis kaniti bu turda ALINMADI ve boyle raporlanir.
    //
    // MK-6: her pin, korudugu alani ONCEKI haline donduren bir mutasyonla sinandi ve TAM 1
    // ISIMLI kirmizi verdi. Sonuclar dalga raporunda.
    public class GuvenlikFix1SozlesmeTests
    {
        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: docker-compose.yml iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pinler artefakti okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen artefakt bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        // ══ YORUM SATIRLARINI DUSUR - CAPA KIRLENMESI KORUMASI ════════════════════════════
        //
        // Bu turda BIREBIR dusuldu: "kara liste `GetOrSetAsync` KULLANMAMALI" pini, yasakladigi
        // dizgeyi ACIKLAMA SATIRINDA tasiyan uretim dosyasi yuzunden mutasyondan ONCE kirmizi
        // verdi. Ayni aile ARSIV-2'de de kayitli ("NEG capa dizesi belgeye YAZILMAZ").
        // Cozum aciklamayi kirpmak DEGIL - olcumu KODLA sinirlamaktir: aciklama, kusurun
        // gerekcesini tasidigi icin DEGERLIDIR ve kalmalidir.
        private static string KodSatirlari(string metin) => string.Join("\n",
            metin.Split('\n').Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // Uretim kaynak agacindaki .cs dosyalarinda bir dizgenin GECIS SAYISI.
        private static int UretimdeSay(string dizge)
        {
            var kok = KokDizin.Value;
            var dizinler = new[] { "Divisima.Bussiness", "Divisima.API" };
            var toplam = 0;
            foreach (var dizin in dizinler)
            {
                var tam = Path.Combine(kok, dizin);
                Directory.Exists(tam).Should().BeTrue($"uretim dizini bulunmali: {dizin}");
                foreach (var dosya in Directory.EnumerateFiles(tam, "*.cs", SearchOption.AllDirectories))
                {
                    // Derleme artiklari sayima GIRMEZ - yoksa ayni satir iki kez sayilirdi.
                    var goreli = Path.GetRelativePath(kok, dosya).Replace('\\', '/');
                    if (goreli.Contains("/obj/") || goreli.Contains("/bin/")) continue;

                    var metin = File.ReadAllText(dosya);
                    var i = 0;
                    while ((i = metin.IndexOf(dizge, i, StringComparison.Ordinal)) >= 0)
                    {
                        toplam++;
                        i += dizge.Length;
                    }
                }
            }
            return toplam;
        }

        // ── K4 NEGATIF KONTROLU ────────────────────────────────────────────────────────────
        //
        // K4 UC sahiplik noktasini 403'ten 404'e cekti. Bu pin, dokunulmamasi gereken
        // ROL / CSRF / IP 403'lerinin DEGISMEDIGINI olcer: "her 403'u 404 yap" YANLIS
        // duzeltmedir - kilitli hesap, dogrulanmamis e-posta, askiya alinmis satici, satici
        // kayit kapisi, antiforgery ve webhook IP allowlist'i 403 KALMALIDIR.
        //
        // OLCUM IKI DESENI BIRDEN KAPSAR (ilk sayimimda ikincisini KACIRMISTIM ve merkezin
        // "11" sayisiyla ayrisiyordum - iki desen birlikte tam 11 veriyor):
        //   `HttpStatusCode.Forbidden`      -> is katmani  (8)
        //   `StatusCodes.Status403Forbidden`-> filtre/middleware (3)
        [Fact]
        public void K4_KALAN_403_YUZEYI_SABIT_KALIR_ROL_CSRF_IP()
        {
            var isKatmani = UretimdeSay("HttpStatusCode.Forbidden");
            var altyapi = UretimdeSay("StatusCodes.Status403Forbidden");

            // SUZGEC AYIRT EDICILIGI (SDP 1.7/1): bilinen-POZITIF ve bilinen-NEGATIF.
            isKatmani.Should().BeGreaterThan(0, "POZ kontrol: sayaci gercekten esleme buluyor");
            UretimdeSay("HttpStatusCode.ForbiddenZZZ").Should().Be(0, "NEG kontrol: uydurma desen 0 dondurmeli");

            (isKatmani + altyapi).Should().Be(11,
                "GF-1 sonrasi uretimdeki 403 yuzeyi TAM 11 olmali (is katmani 8 + altyapi 3). "
                + $"Olculen: is={isKatmani} altyapi={altyapi}. "
                + "ARTARSA: yeni bir sahiplik ihlali 403 donuyor olabilir (sozlesme 404). "
                + "AZALIRSA: rol/CSRF/IP korumalarindan biri kaldirilmis olabilir.");

            // ALAN BAZLI (P19 dersi): sayinin dogru olmasi yetmez - K4'un DOKUNDUGU uc mesaj
            // sabiti GERCEKTEN gitmis olmali, yoksa "11" tesadufen de tutabilirdi.
            var mesajlar = Oku("Divisima.Bussiness/Constants/Messages.cs");
            mesajlar.Should().NotContain("PaymentNotYourOrder",
                "sahiplik ihlali mesaji KALDIRILDI - birakilirsa sizinti geri getirilebilir");
            mesajlar.Should().NotContain("ReturnNotYourOrder",
                "sahiplik ihlali mesaji KALDIRILDI - birakilirsa sizinti geri getirilebilir");
        }

        // K4'un uc noktasi KAYNAKTA da 404'e bagli olmali (davranis kaniti IDOR testinde).
        //
        // SATIR SONU BAGIMSIZ - OLCULDU: uc dosya da SAF LF (CR bayti 0). Capaya `\r\n` ya da
        // `Environment.NewLine` konsaydi pin Windows'ta YALANCI KIRMIZI verirdi. Bu yuzden
        // yuklem ile donus AYRI aranir ve aralarindaki mesafeye bakilir.
        [Theory]
        [InlineData("Divisima.Bussiness/Concrete/ReturnManager.cs", "if (order.customer_id != dto.customer_id)")]
        [InlineData("Divisima.Bussiness/Concrete/IyzicoPaymentManager.cs", "if (order.customer_id != authenticatedCustomerId)")]
        [InlineData("Divisima.Bussiness/Concrete/OrderManager.cs", "if (addr == null || addr.customer_id != dto.customer_id)")]
        public void K4_UC_SAHIPLIK_NOKTASI_KAYNAKTA_404_DONER(string yol, string yuklem)
        {
            var metin = Oku(yol);

            var i = metin.IndexOf(yuklem, StringComparison.Ordinal);
            i.Should().BeGreaterThanOrEqualTo(0, $"{yol}: sahiplik yuklemi kaynakta bulunmali - "
                + "bulunamiyorsa yuklem YENIDEN YAZILMIS demektir ve bu pin ARTIK OLCMUYOR");

            // Yuklemden hemen SONRAKI donus ifadesi. 200 karakter, tek `return` satirini
            // rahatca kapsar ama bir sonraki dala TASMAZ.
            var pencere = metin.Substring(i, Math.Min(200, metin.Length - i));
            pencere.Should().Contain("HttpStatusCode.NotFound",
                $"{yol}: sahiplik ihlali 404 donmeli (SecureControllerBase'teki tek sozlesme)");

            // ALAN BAZLI: dosyada 403 KALMAMALI - "404 eklendi ama 403 da duruyor" hali
            // yukaridaki pencere assert'inden KACARDI.
            metin.Should().NotContain("HttpStatusCode.Forbidden",
                $"{yol}: K4 sonrasi bu dosyada 403 KALMAMALI");
        }

        // ── K6 (C-4) ZAMANLAMA: HER YOL AYNI MALIYETI ODER ─────────────────────────────────
        //
        // GUVENLIK-FIX-2/#19'un kapattigi oracle sinifi, K6 dikkatsiz yapilsaydi GERI GELIRDI:
        // v2'ye gecmis hesap 100k iterasyon oderken v1'de kalmis GERCEK hesap mikrosaniyede
        // yanitlansaydi, HIZLI YANIT "bu hesap eski/kayitli" bilgisini ele verirdi. Ayni sey
        // kayitsiz adreste kosan KUKLA dogrulama icin de gecerli.
        //
        // DAVRANIS KANITI DA VAR (bu tur, .NET 8 uretim kod yolu, 5 tekrar):
        //   v2 ort 32,5 ms  ·  v1 ort 33,2 ms  -> fark olcum gurultusu icinde.
        // Bu pin o davranisin KAYNAK sozlesmesini korur: uc yolun UCU de turetmeyi cagirmali.
        [Fact]
        public void K6_DOGRULAMA_HER_DALDA_AYNI_MALIYETI_ODER()
        {
            var kod = KodSatirlari(Oku("Divisima.Core/Security/Hashing/HashingHelper.cs"));

            // `Turet(` cagrilari: 0-bayt dali + v2 dali + v1 dalinin ESITLEYICISI + uretim
            // (CreatePasswordHash) = 4. Bu sayi 3'e duserse esitleyici SOKULMUS demektir.
            var turetCagrilari = kod.Split("Turet(").Length - 1;
            turetCagrilari.Should().Be(5,
                "dort cagri yeri + bir tanim bekleniyor: uretim, 0-bayt dali, v2 dali, v1 "
                + "ESITLEYICISI ve metodun kendi tanimi. Azalirsa zamanlama kanali ACILIR.");

            // ALAN BAZLI: v1 dali esitleyiciyi ACIKCA cagirmali (sayi tesadufen tutabilir).
            kod.Should().Contain("_ = Turet(password, KuklaTuz, Iterasyon);",
                "v1 dali ve 0-bayt dali sonucu ATILAN bir turetme kosmali - yoksa hizli yanit "
                + "hesabin eski/anonimlestirilmis oldugunu ELE VERIR");

            // NEG kontrol.
            (kod.Split("TuretZZZ(").Length - 1).Should().Be(0, "NEG kontrol: uydurma dizge 0");
        }

        // ── K6 (C-4) SELLER: YAZIM YOK (merkez sarti - Seller DOKUNULMAZ) ──────────────────
        //
        // Paylasilan yardimci yuzunden satici KAYIT yolu artik v2 uretir (kacinilmaz ve
        // zararsiz: `sellers` 0 SATIR, modul veri duzeyinde kapali - `00a:92`). Yasak olan sey
        // satici DOGRULAMA yoluna sessiz YENIDEN YAZIM eklemektir.
        [Fact]
        public void K6_SELLER_YOLUNA_SESSIZ_YENIDEN_YAZIM_EKLENMEZ()
        {
            var seller = KodSatirlari(Oku("Divisima.Bussiness/Concrete/SellerAuthManager.cs"));
            seller.Should().NotContain("SurumGuncelGerekiyorMu",
                "Seller GF-1'de DOKUNULMAZ - surum yukseltme YALNIZ musteri login yolunda");

            // POZ kontrol: musteri yolunda GERCEKTEN var (yoksa bu NotContain bedava dogru olurdu).
            KodSatirlari(Oku("Divisima.Bussiness/Concrete/AuthManager.cs"))
                .Should().Contain("SurumGuncelGerekiyorMu",
                    "musteri login yolu v1 kayitlari sessizce v2'ye tasimali");
        }

        // ── K3 (C-2) 2FA BACAGI - KAYNAK SOZLESMESI ────────────────────────────────────────
        //
        // NEDEN DAVRANIS PINI DEGIL (durust kayit): `two_factor_enabled` uretimde HICBIR kod
        // yolunda `true` yapilmiyor (AuthManager'in kendi aciklamasi da bunu soyluyor:
        // "bugun zaten ulasilamaz bir dal"). Uctan uca olcum icin bayragi DB'den elle acmak
        // gerekirdi - o da URUN DAVRANISINI degil KURGUYU olcerdi. Bu yuzden 2FA bacagi
        // KAYNAK duzeyinde pinleniyor ve davranis kaniti OLMADIGI ACIKCA yaziliyor.
        //
        // OLCULEN SOZLESME: `IssueSessionAndTokenAsync` UC yolu birden besliyor.
        //   login (:329) ve 2FA (:365) -> TEK argumanla cagrilir => auth_time = SIMDI
        //   refresh (:487)             -> `session.auth_time` ile cagrilir => TASINIR
        // Ikisi de KIMLIK DOGRULAMADIR; 2FA sonrasi step-up'in acilmasi DOGRUDUR.
        [Fact]
        public void K3_GIRIS_ve_2FA_AUTH_TIME_I_TASIMAZ_REFRESH_TASIR()
        {
            var kod = KodSatirlari(Oku("Divisima.Bussiness/Concrete/AuthManager.cs"));

            // login + 2FA: TASIMAYAN cagri TAM IKI kez.
            var tasimayan = kod.Split("IssueSessionAndTokenAsync(customer);").Length - 1;
            tasimayan.Should().Be(2,
                "login ve 2FA yollarinin IKISI de auth_time'i SIMDI yapmali (kimlik dogrulama); "
                + "bu sayi 3 olursa refresh de sifirliyor demektir - C-2 GERI GELMIS olur");

            // refresh: TASIYAN cagri TAM BIR kez.
            (kod.Split("IssueSessionAndTokenAsync(customer, session.auth_time);").Length - 1)
                .Should().Be(1, "refresh rotasyonu ESKI giris anini TASIMALI");

            // NEG kontrol: sayaclar gercekten dizgeye bagli.
            (kod.Split("IssueSessionAndTokenAsyncZZZ(customer);").Length - 1)
                .Should().Be(0, "NEG kontrol: uydurma dizge 0 dondurmeli");
        }

        // ── K5 (a) VARSAYILAN-KAPALI KURALIN TASIYICISI ────────────────────────────────────
        //
        // Merkez karari: "MapGet=0" asserti YAZILMAZ (vakum - bugun zaten 0, hicbir sey
        // olmadan yesil kalir). Yerine kuralin GERCEK tasiyicisi pinlenir.
        [Fact]
        public void K5a_MapControllers_RequireAuthorization_ILE_BAGLI()
        {
            var program = Oku("Divisima.API/Program.cs");

            // ANKRAJLI SAYIM (MK-8 dersi, bu turda BIREBIR dusuldu): ankrajsiz desen
            // `MapControllers().RequireAuthorization()` Program.cs'te **IKI** kez esliyor -
            // biri :640'taki GERCEK cagri, digeri :612'deki ACIKLAMA SATIRI. `app.` oneki ve
            // `;` sonekiyle ankrajlanan desen yalniz cagriyi yakalar.
            var gecis = program.Split("app.MapControllers().RequireAuthorization();").Length - 1;
            gecis.Should().Be(1,
                "varsayilan-kapali kural TEK yerden gelir; kaybolursa TUM controller uclari anonimlesir");

            // AYIRT EDICILIK KANITI: ankrajsiz desen 2, ankrajli desen 1 - yani ankraj
            // GERCEKTEN calisiyor (yorum satirini disarida birakiyor).
            (program.Split("MapControllers().RequireAuthorization()").Length - 1)
                .Should().Be(2, "ankrajsiz desen yorumu da sayar - ankrajin gerekcesi budur");

            // NEG kontrol: sayac gercekten dizgeye bagli.
            (program.Split("app.MapControllers().RequireAuthorizationZZZ();").Length - 1)
                .Should().Be(0, "NEG kontrol: uydurma dizge 0 dondurmeli");
        }

        // ── K5 (b) SignalR HUB'I ───────────────────────────────────────────────────────────
        //
        // `Program.cs:641` `app.MapHub<NotificationHub>(...)` cagrisi RequireAuthorization
        // TASIMAZ; korumanin TEK kaynagi hub SINIFININ uzerindeki [Authorize]. Bu yuzden
        // `SecurityHardeningTests`in controller taramasi bu yuzeyi GORMEZ (o tarama
        // `.OfType<ControllerActionDescriptor>()` ile suzuyor) - bosluk BURADA kapaniyor.
        //
        // YANSIMAYLA olculuyor, kaynak metniyle DEGIL: oznitelik gercekten TIPE bagli mi
        // sorusu metin aramasindan daha guclu yanitlanir.
        [Fact]
        public void K5b_NotificationHub_SINIF_UZERINDE_Authorize_TASIR()
        {
            var hubTipi = typeof(Program).Assembly.GetTypes()
                .SingleOrDefault(t => t.Name == "NotificationHub");
            hubTipi.Should().NotBeNull("NotificationHub uretim derlemesinde bulunmali");

            var oznitelikler = hubTipi!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false);
            oznitelikler.Should().NotBeEmpty(
                "hub'a anonim baglanti YOK - koruma SINIF uzerindeki [Authorize] ile saglanir; "
                + "MapHub cagrisi RequireAuthorization tasimaz, bu yuzden tek kaynak budur");

            hubTipi.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
                .Should().BeEmpty("hub ACIKCA anonim isaretlenmis OLMAMALI");

            // VAKUM KIRICI: yansima gercekten ayirt ediyor mu - oznitelik TASIMAYAN bir tip
            // uzerinde ayni sorgu BOS donmeli.
            typeof(GuvenlikFix1SozlesmeTests)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Should().BeEmpty("tarama oznitelik TASIMAYAN tipi dogru gormeli");
        }

        // ── K2 (C-1) KARA LISTE: OKUMA YOLU YAZMAZ ─────────────────────────────────────────
        //
        // OLCULEN KUSUR: `IsRevokedAsync` `GetOrSetAsync` ile okuyup anahtara `false` YAZIYORDU;
        // `RevokeAsync` de ayni `GetOrSetAsync`i kullandigi icin DOLU anahtari EZEMIYORDU.
        // Yani iptal, yazma tarafi baglansa BILE sessiz no-op olurdu. Bu pin, kusurun
        // SINIFINI (okuma yolunda cache-aside) geri gelmesini engeller - davranis kaniti
        // `AccessTokenIptalTests`tedir.
        [Fact]
        public void K2_KARA_LISTE_OKUMA_YOLU_CACHE_ASIDE_KULLANMAZ()
        {
            // YALNIZ KOD taranir: dosyanin aciklamasi kusurun gerekcesini anlatirken yasakli
            // dizgeyi ANMAK ZORUNDA ve o anma bir kusur DEGILDIR (bkz. KodSatirlari).
            var kaynak = KodSatirlari(Oku("Divisima.Core/Security/JWT/CacheTokenBlacklist.cs"));

            // AYIRT EDICILIK: ham metinde dizge GECIYOR (aciklamada), kodda GECMIYOR.
            // Bu satir suzgecin gercekten calistigini KANITLAR - yoksa "0 bulundu" sonucu
            // "dosya okunamadi"dan da gelebilirdi.
            Oku("Divisima.Core/Security/JWT/CacheTokenBlacklist.cs").Should().Contain("GetOrSetAsync",
                "POZ kontrol: dizge dosyada (aciklamada) GERCEKTEN var - suzgec onu KOD sanmamali");

            kaynak.Should().NotContain("GetOrSetAsync",
                "kara listenin HICBIR yolu cache-aside kullanmamali: `GetOrSetAsync` okurken YAZAR "
                + "ve anahtari `false` ile zehirler - iptal o andan sonra ezilemez hale gelir");

            // POZ kontrol: dosya GERCEKTEN okundu ve beklenen primitifler yerinde.
            kaynak.Should().Contain("ExistsAsync",
                "okuma yolu SALT-OKUMA primitifini kullanmali");
            kaynak.Should().Contain("TryAddAsync",
                "yazma yolu ATOMIK set-if-not-exists kullanmali");

            // Iptalin YAZMA tarafi uretimde GERCEKTEN bagli olmali (once SIFIR cagri vardi).
            // ANKRAJLI: ciplak `RevokeAsync` ACIKLAMA satirlarinda da geciyor - `_tokenBlacklist.`
            // oneki yalniz GERCEK cagriyi yakalar (MK-8 dersi).
            UretimdeSay("_tokenBlacklist.RevokeAsync(").Should().Be(2,
                "cikis ve sifre degisimi yollarinin IKISI de access token'i iptal etmeli");
        }

        // ── K5 (c) HANGFIRE PANOSU ─────────────────────────────────────────────────────────
        //
        // OLCULDU: filtre ZATEN VAR ve admin-only. Merkez tarifi "yoksa bu dalgada eklenir"
        // diyordu - EKLEMEYE GEREK KALMADI, yalniz PINLENDI.
        [Fact]
        public void K5c_HangfireDashboard_ADMIN_ONLY_FILTREYE_BAGLI()
        {
            var program = Oku("Divisima.API/Program.cs");
            program.Should().Contain("UseHangfireDashboard",
                "pano cagrisi kaynakta bulunmali (bu pin onun KORUNDUGUNU olcer)");
            program.Should().Contain("Authorization = new[] { new Divisima.API.Services.HangfireAuthorizationFilter() }",
                "pano ACIKCA yetki filtresine bagli olmali - filtresiz DashboardOptions panoyu HERKESE acar");

            // Filtrenin KENDI kurali: kimlik dogrulanmis OLMALI ve user_type Admin (1) OLMALI.
            var filtre = Oku("Divisima.API/Services/HangfireAuthorizationFilter.cs");
            filtre.Should().Contain("IsAuthenticated != true) return false",
                "kimligi dogrulanmamis istek REDDEDILMELI");
            filtre.Should().Contain("userType == \"1\"",
                "yalniz admin (user_type=1) gecmeli - kimlik dogrulamasi TEK BASINA yetmez");
        }
    }
}
