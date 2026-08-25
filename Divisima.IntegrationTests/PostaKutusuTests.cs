using System.Globalization;
using System.Threading;
using Divisima.Core.Utilities.Text;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GUVENLIK-FIX-4 - KANONIK POSTA KUTUSU ══════════════════════════════════════════════
    //
    // Bu sinif VERITABANI ACMAZ (10d794d dersi: her yeni DB kuran sinif `model` kilidinde bir
    // katilimci daha olur). Saf bir dizge donusumu pinleniyor.
    //
    // OLCULEN GEREKCE: `+etiket` varyanti misafir checkout'un 409 guard'ini ASIYOR ve ayni
    // fiziksel kutuya yigiliyor (kurban@x -> 201, kurban+a@x -> 201). Kotuye kullanim sayaci
    // bu eksende calisir. KIMLIK DEGISMEZ - o sinir `PostaKutusu` dosyasinin basinda.
    public class PostaKutusuTests
    {
        [Theory]
        [InlineData("kullanici@example.com", "kullanici@example.com")]
        [InlineData("kullanici+a@example.com", "kullanici@example.com")]
        [InlineData("kullanici+a+b@example.com", "kullanici@example.com")]
        [InlineData("KULLANICI+Etiket@Example.COM", "kullanici@example.com")]
        [InlineData("  kullanici+x@example.com  ", "kullanici@example.com")]
        public void ETIKET_SIYRILIR_ve_KUCULTULUR(string girdi, string beklenen)
        {
            PostaKutusu.Kanonik(girdi).Should().Be(beklenen);
        }

        // CIFT-ANLAM KIRICI: "her seyi kirp" YANLIS donusumdur. Nokta SIYRILMAZ - bilincli
        // sinir: yalniz BAZI saglayicilar noktayi yok sayar; siyirmak `a.b@x` ile `ab@x`i
        // AYNI kisi sayardi ve iki FARKLI musteriyi birbirinin esigine yazardi.
        [Theory]
        [InlineData("a.b@example.com", "a.b@example.com")]
        [InlineData("ad.soyad+etiket@example.com", "ad.soyad@example.com")]
        public void NOKTA_SIYRILMAZ_BILINEN_SINIR(string girdi, string beklenen)
        {
            PostaKutusu.Kanonik(girdi).Should().Be(beklenen);
        }

        // VAKUM KIRICI: farkli kutular BIRLESTIRILMEMELI - aksi halde guard mesru
        // musterileri birbirinin esigine yazardi.
        [Fact]
        public void FARKLI_KUTULAR_BIRLESTIRILMEZ()
        {
            PostaKutusu.Kanonik("a@example.com").Should().NotBe(PostaKutusu.Kanonik("b@example.com"));
            PostaKutusu.Kanonik("a@example.com").Should().NotBe(PostaKutusu.Kanonik("a@example.net"));
            PostaKutusu.Kanonik("a.b@example.com").Should().NotBe(PostaKutusu.Kanonik("ab@example.com"));
        }

        // Cozumlenemeyen girdide bir sey UYDURULMAZ; '+' ile BASLAYAN adreste yerel kisim
        // BOSALTILMAZ (yoksa boyle her adres tek kovaya duserdi).
        [Theory]
        [InlineData("", "")]
        [InlineData("duz-metin", "duz-metin")]
        [InlineData("@example.com", "@example.com")]
        [InlineData("+etiket@example.com", "+etiket@example.com")]
        public void COZUMLENEMEYEN_GIRDI_BOZULMAZ(string girdi, string beklenen)
        {
            PostaKutusu.Kanonik(girdi).Should().Be(beklenen);
        }

        // KIMLIK DIZGESI -> KULTURSUZ (CLAUDE.md bolum 6c). tr-TR'de `I` -> `ı` katlanir;
        // kulturlu bir kucultme ayni degerin iki yazimindan IKI FARKLI anahtar uretirdi.
        [Fact]
        public void KULTURDEN_BAGIMSIZ_tr_TR_ALTINDA_da_AYNI_SONUC()
        {
            var onceki = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                var trIle = PostaKutusu.Kanonik("KIRMIZI+etiket@Example.COM");

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                var invariantIle = PostaKutusu.Kanonik("KIRMIZI+etiket@Example.COM");

                trIle.Should().Be(invariantIle, "kimlik dizgesi KULTURSUZ kucultulmeli");
                trIle.Should().Be("kirmizi@example.com", "noktali i beklenir - tr-TR'nin 'ı'si DEGIL");
            }
            finally { Thread.CurrentThread.CurrentCulture = onceki; }
        }
    }
}
