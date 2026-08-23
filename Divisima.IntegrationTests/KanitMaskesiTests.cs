using Divisima.Core.Utilities.Text;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ KANIT MASKESI - KAYNAGINDA KAPATMA PINI ════════════════════════════════════════════
    //
    // NEDEN: CLAUDE.md bolum 1'in kirpma kurali UC KEZ kirildi (Sprint 8, GUVENLIK-FIX-2,
    // LAUNCH-FIX Dalga A) ve ucunde de bedeli KIRMIZI BIR RUN oldu. Kural insan disiplinine
    // birakilamayacagi icin maskeleme URETIM NOKTASINA tasindi. Bu pinler, maskenin
    // (a) jetonu GERCEKTEN kirptigini, (b) TESHIS DEGERINI korudugunu sabitler.
    //
    // Test verisi UYDURMA DEGIL: her satir gercek olculmus bir ornegin ayni KARAKTER
    // SINIFINDAN uretilmis esdegeri (jetonlarin kendisi depoya girmez - bolum 1).
    public class KanitMaskesiTests
    {
        [Theory]
        // base64url jeton (dogrulama/sifirlama jetonlarinin bicimi): rakam + kucuk harf, 43 krkt
        [InlineData("94aaSsO4Zz9ALIq8ioYZ6MWJPpea5iDNPtDJHOJSQM1w")]
        // JWT bolumu: rakam + kucuk harf
        [InlineData("eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9")]
        // Guid("N") onaltilik: ENTROPISI 3.480 - gitleaks esiginin ALTINDA ama yine de jeton.
        // Bu satir, olcutun neden ENTROPI DEGIL karakter sinifi oldugunun kanitidir.
        [InlineData("3088210327e2498bb72452464e6e449f")]
        public void JETON_BENZERI_DIZGE_ILK_8_KARAKTERE_Kirpilir(string jeton)
        {
            var sonuc = KanitMaskesi.Maskele(jeton);

            sonuc.Should().Be(jeton.Substring(0, 8) + "…");
            sonuc.Should().NotBe(jeton, "maskeleme GERCEKTEN bir sey yapmali");
            // ASIL IDDIA: jetonun KUYRUGU ciktida KALMAMALI.
            sonuc.Should().NotContain(jeton.Substring(8), "jetonun geri kalani SIZMAMALI");
        }

        [Theory]
        // CIFT-ANLAM KIRICI: "her uzun dizgeyi kirp" YANLIS bir uygulama olurdu ve teshis
        // degerini yok ederdi. Asagidakilerin HEPSI olculdu ve GORUNUR kalmali:
        [InlineData("paymentTransactionId")]     // entropi 3.746 ama RAKAM YOK
        [InlineData("email_verification_token")] // entropi 3.637 ama RAKAM YOK
        [InlineData("InternalServerError")]      // entropi 3.076
        [InlineData("DVS20260823-54740CC62D")]   // siparis no: rakam VAR ama KUCUK HARF YOK
        [InlineData("application/json")]
        public void TESHIS_DEGERI_TASIYAN_DIZGELER_DOKUNULMADAN_Gecer(string metin)
        {
            KanitMaskesi.Maskele(metin).Should().Be(metin,
                "maske teshisi korumali - aksi halde kirmizi bir adimin sebebi okunamaz olurdu");
        }

        [Fact]
        public void BAGLANTIDA_ORIGIN_ve_YOL_GORUNUR_KALIR_YALNIZ_JETON_Gider()
        {
            const string jeton = "94aaSsO4Zz9ALIq8ioYZ6MWJPpea5iDNPtDJHOJSQM1w";
            var sonuc = KanitMaskesi.Maskele("http://localhost:5173/#/dogrula/" + jeton);

            // Olcum degeri KAYBOLMAZ: hangi origin, hangi rota - hepsi okunur.
            sonuc.Should().Contain("localhost").And.Contain("/#/dogrula/");
            sonuc.Should().NotContain(jeton, "jeton TAM HALIYLE cikmamali");
            sonuc.Should().Contain("94aaSsO4…", "ilk 8 karakter kanit icin yeter");
        }

        [Fact]
        public void GOVDE_ICINDEKI_JETON_KIRPILIR_CEVRESI_BOZULMAZ()
        {
            var govde = "{\"success\":false,\"token\":\"a7sK1hP5d6NRsIMpexcffLb5ZhdUE2UER66PhWNzp6E\"}";
            var sonuc = KanitMaskesi.Maskele(govde)!;

            sonuc.Should().StartWith("{\"success\":false,\"token\":\"");
            sonuc.Should().EndWith("\"}");
            sonuc.Should().NotContain("PhWNzp6E", "jetonun kuyrugu SIZMAMALI");
            // VAKUM KIRICI: JSON'un okunabilirligi korunuyor - alan adlari maskelenmedi.
            sonuc.Should().Contain("success").And.Contain("token");
        }

        [Fact]
        public void BOS_ve_NULL_GIRDI_AYNEN_Doner()
        {
            KanitMaskesi.Maskele(null).Should().BeNull();
            KanitMaskesi.Maskele("").Should().Be("");
        }

        [Fact]
        public void PAYLASILAN_TEST_YARDIMCISI_MASKEYI_KULLANIR()
        {
            // Kaynagi kapatan asil yer burasi: TestAuthHelper register/verify/LOGIN kosuyor ve
            // basarisiz login yaniti JWT tasiyabilir; mesaji DOGRUDAN CI ciktisina duser.
            var kok = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (kok != null && !System.IO.File.Exists(
                System.IO.Path.Combine(kok.FullName, "Divisima.IntegrationTests", "TestAuthHelper.cs")))
                kok = kok.Parent;
            kok.Should().NotBeNull("depo koku bulunmali - sessiz skip YOK");

            var kaynak = System.IO.File.ReadAllText(
                System.IO.Path.Combine(kok!.FullName, "Divisima.IntegrationTests", "TestAuthHelper.cs"));
            kaynak.Should().Contain("KanitMaskesi.Maskele",
                "paylasilan yardimcinin hata mesaji MASKELENMIS govde tasimali");
        }
    }
}
