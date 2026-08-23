using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ LAUNCH-FIX DALGA A - FRONTEND KAYNAK SOZLESMESI PINLERI (A2 + A4) ═══════════════════
    //
    // BU PINLERIN SINIRI (DURUST KAYIT, Dalga 4'teki ayni sinir): depoda JS/DOM kosucusu YOK.
    // Tarayici semantigi burada dogrulanamaz; bu pinler KAYNAK SOZLESMESINI tutar. Davranis
    // kaniti tarayicida/canli SMTP ile olculdu ve rapora yazildi.
    public class LaunchFixDalgaAFrontendTests
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
        private static string Bridge => Oku("frontend/api-bridge.js");
        private static string Client => Oku("frontend/api-client.js");

        // ── A2: "SIFREMI UNUTTUM" ARTIK OLU LINK DEGIL ───────────────────────────────────
        //
        // OLCULEN ONCE-DURUM: index.html'de <a href="#" data-i18n="forgot">Sifremi unuttum</a>
        // duruyordu; api-client.js'te forgotPassword/resetPassword TANIMLIYDI ama api-bridge.js'te
        // "forgot" 0 kez geciyordu - yani hicbir sey bagli DEGILDI.
        [Fact]
        public void SifremiUnuttum_LINKI_ARTIK_OLU_DEGIL_HANDLERI_VAR_ve_HEDEFI_closest_ILE_COZER()
        {
            // VAKUM KIRICI: once linkin HALA orada oldugu dogrulanir. Link silinseydi asagidaki
            // assert'ler anlamsizca yesil kalirdi.
            Index.Should().Contain("data-i18n=\"forgot\"",
                "pin, index.html'deki 'Sifremi unuttum' baglantisina dayaniyor");

            Bridge.Should().Contain("closest('[data-i18n=\"forgot\"]')",
                "handler hedefi closest ile cozmeli - DALGA 4 / M10'da olculdu ki kati hedef "
                + "karsilastirmasi gercek dokunusta (ripple ink) SESSIZCE dusuyor");
            Bridge.Should().Contain("sifremiUnuttumEkrani",
                "tiklama bir ekran acmali");
            Bridge.Should().Contain("api.auth.forgotPassword",
                "ekran gercek ucu cagirmali - api-client'taki fonksiyon E3'ten beri CAGRILMIYORDU");
        }

        [Fact]
        public void SIFRE_SIFIRLAMA_ve_DOGRULAMA_ROTALARI_ROUTER_SARMASINDA_TANIMLI()
        {
            // Bilinmeyen rota index.html'de show404'e duser; bu iki yol ONCE yakalanmali.
            Index.Should().Contain("else show404();",
                "pin, router'in bilinmeyen yolu 404'e dusurdugu gercegine dayaniyor");

            Bridge.Should().Contain("__dvsAuthWrapped", "router sarmalanmis olmali");
            Bridge.Should().Contain("\"dogrula\"", "#/dogrula/<token> rotasi tanimli olmali");
            Bridge.Should().Contain("\"sifre-sifirla\"", "#/sifre-sifirla/<token> rotasi tanimli olmali");
            Bridge.Should().Contain("api.auth.resetPassword", "sifre sifirlama ucu cagrilmali");
            Bridge.Should().Contain("api.auth.verifyEmail", "dogrulama ucu cagrilmali");

            // CIFT-ANLAM KIRICI: sarmalayici bilinmeyen rotalari ORIJINAL router'a devretmeli.
            // Devretmeseydi tum site iki rotaya inerdi ve yukaridaki assert'ler yine yesil kalirdi.
            Bridge.Should().Contain("origRouter.apply",
                "ozel olmayan her rota orijinal router'a DEVREDILMELI");

            // ILK YUKLEME YARISI (E3/M12'de olculdu): index.html'in router'i sayfa
            // ayristirilirken bir kez kosuyor - sarmalama devreye girmeden.
            Bridge.Should().Contain("ozelAuthRotasi();",
                "sarmalama kurulduktan sonra rota BIR KEZ daha degerlendirilmeli (ilk yukleme yarisi)");
        }

        [Fact]
        public void SIFRE_SIFIRLAMA_UCU_ISTEMCIDE_TANIMLI_ve_TOKEN_ILE_CAGRILIYOR()
        {
            // Cift-anlam kirici: uc api-client'ta ZATEN vardi (E3), eksik olan CAGIRANDI.
            Client.Should().Contain("resetPassword(payload)", "uc istemcide tanimli olmali");
            Client.Should().Contain("/api/auth/reset-password", "dogru yola gitmeli");
            Bridge.Should().Contain("new_password", "yeni sifre govdede gonderilmeli");
        }

        // ── A4: TEK PARA BIRIMI (TRY) ────────────────────────────────────────────────────
        //
        // KULLANICI KARARI: launch'ta TEK para birimi TRY. Secici GIZLENIR (kaldirilmaz).
        [Fact]
        public void KUR_TABLOSU_YALNIZ_TRY_Icerir_SABIT_KUR_KALMADI()
        {
            var i = Index;
            i.Should().Contain("var CUR={TRY:{rate:1,sym:'₺'}};",
                "kur tablosu TRY'ye indirilmis olmali");
            i.Should().NotContain("EUR:{rate:", "bayat sabit EUR kuru kaynakta KALMAMALI");
            i.Should().NotContain("USD:{rate:", "bayat sabit USD kuru kaynakta KALMAMALI");

            // CIFT-ANLAM KIRICI: tabloyu bosaltmak yetmez, tl() icindeki CEVRIM DALI da gitmeli.
            // Kalsaydi tablo geri geldigi gun ayni ayrisma geri gelirdi.
            i.Should().NotContain("(n/c.rate)", "doviz cevrimi dali tl() icinde KALMAMALI");
        }

        [Fact]
        public void PARA_BICIMI_TEK_KAYNAK_api_bridge_INDEXTEKI_tl_e_DELEGE_EDER()
        {
            var b = Bridge;
            // OLCULEN ONCE-DURUM: api-bridge.js'te "tl(" 0 kez geciyordu; bu dosyanin cizdigi
            // odeme paneli / siparis listesi / faturalar KENDI bicimleyicisini kullaniyordu.
            b.Should().Contain("window.tl(Number(n || 0))", "money() tl()'e delege etmeli");
            b.Should().Contain("window.tl(Number(n) || 0)", "paraTL() tl()'e delege etmeli");

            // VAKUM KIRICI: iki bicimleyici de HALA VAR ve kullaniliyor olmali - biri silinseydi
            // "delege ediyor" iddiasi bos kalirdi.
            b.Should().Contain("function money(n)");
            b.Should().Contain("function paraTL(n)");
        }

        [Fact]
        public void DOVIZ_SECICISI_GIZLENIR_ama_MARKUP_DEPODA_DURUR()
        {
            // Kullanicinin sarti: "kaldirma, gizle - ileride gercek kur servisiyle doner".
            Index.Should().Contain("data-cur=\"USD\"", "markup KALMALI (gizlenir, silinmez)");
            Index.Should().Contain("id=\"curSelect\"", "markup KALMALI (gizlenir, silinmez)");

            var b = Bridge;
            b.Should().Contain("wireParaBirimi", "gizleme kablolamasi bulunmali");
            b.Should().Contain("\"curbox\", \"curSelect\"", "iki secici de gizlenmeli");
            b.Should().Contain("localStorage.removeItem(\"dvs_cur\")",
                "eski oturumdan kalan USD/EUR secimi TEMIZLENMELI - aksi halde secici geri "
                + "acildigi gun kullanici sessizce dovize donerdi");
        }
    }
}
