using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ A3 HIBRIT - FRONTEND KAYNAK SOZLESMESI PINLERI ═════════════════════════════════════
    //
    // SINIR (Dalga 4'ten beri ayni durust kayit): depoda JS/DOM kosucusu YOK; bu pinler KAYNAK
    // SOZLESMESINI tutar, tarayici semantigini degil. Davranis kaniti dalga raporundaki elle
    // dogrulama bloklarinda (misafir formu cizildi, kart secenegi disabled, siparis #91 olustu).
    public class MisafirA3FrontendTests
    {
        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "frontend", "index.html")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: frontend/index.html iceren ust dizin yok. Sessiz skip YOK.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen kaynak dosya bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        private static string Index => Oku("frontend/index.html");
        private static string Bridge => Oku("frontend/api-bridge.js");
        private static string Client => Oku("frontend/api-client.js");

        [Fact]
        public void MISAFIR_FORMU_VAR_ve_UC_ISTEMCIDE_TANIMLI()
        {
            // OLCULEN ONCE-DURUM: guest-checkout cagrisi index.html 0, api-bridge 0, api-client 0.
            Client.Should().Contain("/api/guest-checkout/place", "uc istemcide tanimli olmali");
            Client.Should().Contain("placeAsGuest", "cagrilabilir bir ad almali");

            var b = Bridge;
            b.Should().Contain("misafirCheckoutCiz", "cikisli kullaniciya misafir formu cizilmeli");
            b.Should().Contain("api.orders.placeAsGuest", "form gercek ucu cagirmali");
            b.Should().Contain("payment_method: 1", "misafir YALNIZ kapida odeme gonderir");
        }

        [Fact]
        public void KART_SECENEGI_MISAFIRE_KAPALI_ve_NEDENI_GORUNUR()
        {
            var b = Bridge;
            // Sessizce gizlemek, kullaniciya NEDEN secemedigini soylememek olurdu.
            b.Should().Contain("Kartla ödeme için", "neden GORUNUR olmali");
            b.Should().Contain("üye girişi", "cozum yolu da soylenmeli");
            b.Should().Contain("name=\"mgOdeme\"", "odeme secenekleri cizilmeli");
            b.Should().Contain("disabled", "kart secenegi SECILEMEZ olmali");
        }

        [Fact]
        public void SSS_VAADI_DAVRANISLA_UYUMLU()
        {
            var i = Index;
            // VAKUM KIRICI: soru HALA duruyor olmali - silinseydi assert anlamsizca yesil kalirdi.
            i.Should().Contain("Üye olmadan sipariş verebilir miyim?");
            // Vaat artik GERCEGI soylemeli: misafir VAR ama kapida odeme ile.
            i.Should().Contain("misafir siparişleri kapıda ödeme ile alınır",
                "SSS bugunku davranisi anlatmali - eski metin kart imasi tasiyordu");
            i.Should().Contain("guest orders are paid cash on delivery", "Ingilizce karsiligi da");
        }

        [Fact]
        public void OLU_co_guest_ANAHTARLARI_YENI_FORMA_BAGLANDI()
        {
            // OLCULDU: .co-guest blogu index.html'in mock checkout'unda (coStep1) duruyor ve
            // E2 paneli onun USTUNE yaziyor -> DOM'da hic gorunmuyordu. Ceviriler (tr/en/ar)
            // ZATEN vardi; silmek yerine yeni forma baglandilar.
            Index.Should().Contain("co_guest_t:", "ceviri anahtarlari KORUNMALI");
            var b = Bridge;
            b.Should().Contain("t(\"co_guest_t\")", "olu anahtar yeni formda KULLANILMALI");
            b.Should().Contain("t(\"co_guest_login\")");
        }

        [Fact]
        public void MISAFIR_SONUC_EKRANI_ULASILAMAYAN_YOL_GOSTERMEZ()
        {
            var b = Bridge;
            // M11 dersi: hedefteki eylem GERCEKTEN kullanilabilir olmali. Misafirin oturumu yok,
            // "Siparislerime git" ona bos/401 verirdi.
            b.Should().Contain("params.guest", "misafir oldugu URL'den OKUNMALI, tahmin edilmemeli");
            b.Should().Contain("misafirMi ? '<a class=\"btn\" href=\"#/giris\">Şifre belirle</a>'",
                "misafire CALISAN bir yol gosterilmeli");
        }
    }
}
