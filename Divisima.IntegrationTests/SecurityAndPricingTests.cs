using System;
using System.Collections.Generic;
using System.Linq;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Security.Tokens;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Sanitization;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Güvenlik-kritik saf fonksiyonların GERÇEK C# testleri (I/O yok, hızlı).
    // Kripto token üreteci, şifre hash'leme, XSS temizleme, SSRF koruması, indirim penceresi.
    public class SecureTokenGeneratorTests
    {
        [Fact]
        public void Generate_ProducesUniqueTokens()
        {
            // 1000 token üret - hepsi benzersiz olmalı (çakışma = zayıf entropi)
            var tokens = Enumerable.Range(0, 1000).Select(_ => SecureTokenGenerator.Generate()).ToList();
            tokens.Distinct().Count().Should().Be(1000);
        }

        [Fact]
        public void Generate_IsUrlSafe_NoPlusSlashEquals()
        {
            // URL/e-posta'da taşınır: +, /, = OLMAMALI
            for (int i = 0; i < 100; i++)
            {
                var token = SecureTokenGenerator.Generate();
                token.Should().NotContain("+");
                token.Should().NotContain("/");
                token.Should().NotContain("=");
            }
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public void Generate_RespectsEntropyLength(int byteLength)
        {
            // base64: her 3 bayt -> 4 karakter. En az byteLength kadar entropi taşımalı.
            var token = SecureTokenGenerator.Generate(byteLength);
            token.Length.Should().BeGreaterOrEqualTo(byteLength);   // padding kırpıldığı için >=
        }
    }

    public class HashingHelperTests
    {
        [Fact]
        public void CreatePasswordHash_UsesRandomSalt_DifferentEachTime()
        {
            // Aynı şifre iki kez hash'lenince salt (ve dolayısıyla hash) FARKLI olmalı (rainbow-table savunması)
            HashingHelper.CreatePasswordHash("AyniSifre123", out var hash1, out var salt1);
            HashingHelper.CreatePasswordHash("AyniSifre123", out var hash2, out var salt2);
            salt1.Should().NotBeEquivalentTo(salt2);
            hash1.Should().NotBeEquivalentTo(hash2);
        }

        [Fact]
        public void VerifyPasswordHash_RoundTrip_Succeeds()
        {
            HashingHelper.CreatePasswordHash("DogruSifre!", out var hash, out var salt);
            HashingHelper.VerifyPasswordHash("DogruSifre!", hash, salt).Should().BeTrue();
        }

        [Fact]
        public void VerifyPasswordHash_WrongPassword_Fails()
        {
            HashingHelper.CreatePasswordHash("DogruSifre!", out var hash, out var salt);
            HashingHelper.VerifyPasswordHash("YanlisSifre!", hash, salt).Should().BeFalse();
        }

        // ══ GF-1 / K6 (C-4) PINLERI ════════════════════════════════════════════════════════
        //
        // 64/128 SOZLESMESI BU TURA KADAR HICBIR PINLE KORUNMUYORDU (olculdu: uzunluk assert'i
        // 0). Yani algoritma degisse ustteki uc test de YESIL kalirdi - "round-trip calisiyor"
        // demek "is faktoru var" demek DEGILDIR.

        // v1 kaydini URETIMDEKI eski algoritmayla (HMAC-SHA512) kurar. Bu, testin kendi
        // fikstur uretecidir - uretim kodu ARTIK boyle yazmiyor, ama boyle YAZILMIS kayitlari
        // DOGRULAMAYA devam etmek ZORUNDA.
        private static void V1KaydiUret(string sifre, out byte[] hash, out byte[] tuz)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            tuz = hmac.Key;
            hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sifre));
        }

        [Fact]
        public void K6_V1_KAYITLARI_BAYT_DEGISMEZ_DOGRULANMAYA_DEVAM_EDER()
        {
            V1KaydiUret("EskiSifre!1", out var hash, out var tuz);

            // SOZLESME PINI: eski bicim 64/128 (bu tura kadar pinsizdi).
            hash.Length.Should().Be(HashingHelper.BeklenenV1HashUzunlugu, "v1 hash 64 bayt olmali");
            tuz.Length.Should().Be(HashingHelper.BeklenenV1TuzUzunlugu, "v1 tuz 128 bayt olmali");

            var hashKopya = (byte[])hash.Clone();
            var tuzKopya = (byte[])tuz.Clone();

            HashingHelper.VerifyPasswordHash("EskiSifre!1", hash, tuz).Should().BeTrue(
                "v1 kayitlari DOGRULANMAYA devam etmeli - aksi halde mevcut TUM kullanicilar kilitlenirdi");
            HashingHelper.VerifyPasswordHash("YanlisSifre!1", hash, tuz).Should().BeFalse(
                "v1 yolunda yanlis sifre yine REDDEDILMELI");

            // BAYT-DEGISMEZ: dogrulama kaydi YENIDEN YAZMAZ (yeniden yazim kararI cagirandadir).
            hash.Should().Equal(hashKopya, "dogrulama hash baytlarina DOKUNMAMALI");
            tuz.Should().Equal(tuzKopya, "dogrulama tuz baytlarina DOKUNMAMALI");

            HashingHelper.SurumGuncelGerekiyorMu(hash).Should().BeTrue(
                "v1 kaydi 'guncellenmeli' olarak isaretlenmeli - login sessizce v2'ye tasir");
        }

        [Fact]
        public void K6_YENI_KAYITLAR_V2_ZARFI_URETIR_ve_GUNCELLEME_ISTEMEZ()
        {
            HashingHelper.CreatePasswordHash("YeniSifre!1", out var hash, out var tuz);

            hash.Length.Should().Be(HashingHelper.BeklenenV2HashUzunlugu,
                "v2 zarfi surum bayti + iterasyon + 64 baytlik anahtar tasimali");
            tuz.Length.Should().Be(HashingHelper.BeklenenV2TuzUzunlugu, "v2 tuzu 16 bayt olmali");

            // AYIRT EDICILIK: v2 uzunlugu v1'inkiyle CAKISMAMALI - zarf ayrimi UZUNLUKLA kesin.
            hash.Length.Should().NotBe(HashingHelper.BeklenenV1HashUzunlugu,
                "v1 ve v2 uzunluklari AYRISMALI, yoksa surum tespiti onek baytina (1/256) kalirdi");

            // IS FAKTORU zarfin ICINDE ve merkezin verdigi degerde.
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(1, 4))
                .Should().Be(HashingHelper.BeklenenIterasyon, "iterasyon sayisi zarfta TASINMALI");
            HashingHelper.BeklenenIterasyon.Should().Be(100_000, "merkez karari: 100k");

            HashingHelper.VerifyPasswordHash("YeniSifre!1", hash, tuz).Should().BeTrue();
            HashingHelper.SurumGuncelGerekiyorMu(hash).Should().BeFalse("v2 kaydi zaten guncel");
        }

        // KVKK anonimlestirmesi hash/salt alanlarini BOSALTIYOR (olculdu: 6 satir, hepsi
        // is_active=0). O satirlar PATLAMAMALI ve HICBIR sifreyle eslesMEMELI.
        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 16)]
        [InlineData(69, 0)]
        public void K6_SIFIR_BAYT_KAYITLAR_GUVENLI_RED_PATLAMAZ(int hashUzunlugu, int tuzUzunlugu)
        {
            var hash = new byte[hashUzunlugu];
            var tuz = new byte[tuzUzunlugu];

            Action cagri = () => HashingHelper.VerifyPasswordHash("HerhangiSifre1", hash, tuz);
            cagri.Should().NotThrow("anonimlestirilmis kayit istisna FIRLATMAMALI");
            HashingHelper.VerifyPasswordHash("HerhangiSifre1", hash, tuz).Should().BeFalse(
                "bos hash/tuz HICBIR sifreyle eslesMEMELI");
            HashingHelper.SurumGuncelGerekiyorMu(Array.Empty<byte>()).Should().BeFalse(
                "anonimlestirilmis kayit yeniden yazilmaya CALISILMAMALI");
        }

        // NULL de patlatmamali (savunma derinligi - cagiranlar entity alanlarini dogrudan geciriyor).
        [Fact]
        public void K6_NULL_GIRDI_GUVENLI_RED_PATLAMAZ()
        {
            Action cagri = () => HashingHelper.VerifyPasswordHash("x", null!, null!);
            cagri.Should().NotThrow();
            HashingHelper.VerifyPasswordHash("x", null!, null!).Should().BeFalse();
        }
    }

    public class InputSanitizerExtraTests
    {
        [Theory]
        [InlineData("<script>alert('xss')</script>Merhaba")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("javascript:alert(document.cookie)")]
        public void Sanitize_RemovesDangerousContent(string malicious)
        {
            var clean = InputSanitizer.Sanitize(malicious);
            // Script etiketi ve tehlikeli şema temizlenmeli
            clean.ToLowerInvariant().Should().NotContain("<script");
            clean.ToLowerInvariant().Should().NotContain("javascript:");
        }

        [Fact]
        public void Sanitize_PreservesNormalText()
        {
            // Zararsız metin bozulmamalı
            InputSanitizer.Sanitize("Ayşe Yılmaz").Should().Contain("Ayşe");
        }
    }

    public class UrlValidatorExtraTests
    {
        [Theory]
        [InlineData("https://divisima.com/callback")]
        [InlineData("https://api.iyzico.com/payment")]
        public void IsSafePublicHttpsUrl_AllowsPublicHttps(string url)
        {
            UrlValidator.IsSafePublicHttpsUrl(url).Should().BeTrue();
        }

        [Theory]
        [InlineData("http://divisima.com")]              // https değil
        [InlineData("https://localhost/x")]              // yerel (SSRF)
        [InlineData("https://127.0.0.1/x")]              // loopback (SSRF)
        [InlineData("https://192.168.1.1/admin")]        // özel ağ (SSRF)
        [InlineData("https://169.254.169.254/latest")]   // cloud metadata (SSRF - kritik)
        [InlineData("ftp://divisima.com")]               // https değil
        [InlineData("not-a-url")]
        public void IsSafePublicHttpsUrl_BlocksUnsafeAndInternal(string url)
        {
            // SSRF savunması: sadece dış HTTPS; yerel/özel/metadata adresleri reddedilmeli
            UrlValidator.IsSafePublicHttpsUrl(url).Should().BeFalse();
        }
    }

    public class PricingHelperExtraTests
    {
        private static readonly DateTime Now = new DateTime(2026, 7, 20, 12, 0, 0);

        [Fact]
        public void EffectivePrice_NoSale_ReturnsFullPrice()
        {
            PricingHelper.EffectivePrice(1200m, null, null, null, Now).Should().Be(1200m);
        }

        [Fact]
        public void EffectivePrice_ActiveSaleWindow_ReturnsSalePrice()
        {
            var start = Now.AddDays(-1);
            var end = Now.AddDays(1);
            PricingHelper.EffectivePrice(1200m, 900m, start, end, Now).Should().Be(900m);
        }

        [Fact]
        public void EffectivePrice_ExpiredSale_ReturnsFullPrice()
        {
            // İndirim penceresi geçmiş -> tam fiyat (indirim sızması olmamalı)
            var start = Now.AddDays(-10);
            var end = Now.AddDays(-1);
            PricingHelper.EffectivePrice(1200m, 900m, start, end, Now).Should().Be(1200m);
        }

        [Fact]
        public void IsOnSale_ActiveWindow_True()
        {
            PricingHelper.IsOnSale(900m, Now.AddDays(-1), Now.AddDays(1), Now).Should().BeTrue();
        }

        [Fact]
        public void IsOnSale_NoSalePrice_False()
        {
            PricingHelper.IsOnSale(null, Now.AddDays(-1), Now.AddDays(1), Now).Should().BeFalse();
        }
    }
}
