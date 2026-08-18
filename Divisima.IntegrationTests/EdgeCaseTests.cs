using System;
using Divisima.Core.Utilities.Pricing;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Para hesaplarinin SINIR-DEGER testleri (kurus sapmasi / yuvarlama regresyonlari).
    // Bu testler gercek insanlarin urettigi tuhaf tutarlari (0.005, negatif, cok buyuk) modeller.
    public class MoneyHelperEdgeTests
    {
        [Theory]
        [InlineData("0", "0.00")]
        [InlineData("2.005", "2.01")]     // midpoint away-from-zero (banker's 2.00 verirdi)
        [InlineData("2.015", "2.02")]
        [InlineData("-2.005", "-2.01")]   // negatif de away-from-zero
        [InlineData("100.999", "101.00")]
        [InlineData("0.001", "0.00")]
        [InlineData("999999.995", "1000000.00")]
        public void Round_EdgeCases(string input, string expected)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            MoneyHelper.Round(decimal.Parse(input, ci)).Should().Be(decimal.Parse(expected, ci));
        }

        [Fact]
        public void Add_EmptyArray_ReturnsZero()
        {
            MoneyHelper.Add().Should().Be(0m);
        }

        [Fact]
        public void Add_AccumulatesAndRounds()
        {
            // Birikimli kurus hatasi onleme: 0.005 + 0.005 = 0.01
            MoneyHelper.Add(0.005m, 0.005m).Should().Be(0.01m);
            MoneyHelper.Add(1.111m, 2.222m, 3.333m).Should().Be(6.67m);  // 6.666 -> 6.67
        }

        [Theory]
        [InlineData("100", "0", "0.00")]     // %0
        [InlineData("100", "100", "100.00")] // %100
        [InlineData("100", "150", "150.00")] // %150 (tavan helper'da degil, kupon mantiginda uygulanir)
        [InlineData("0", "50", "0.00")]      // sifir baz
        [InlineData("33.33", "33", "11.00")] // 33.33*0.33 = 10.9989 -> 11.00
        public void Percentage_EdgeCases(string baseAmount, string percent, string expected)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            MoneyHelper.Percentage(decimal.Parse(baseAmount, ci), decimal.Parse(percent, ci))
                .Should().Be(decimal.Parse(expected, ci));
        }

        [Fact]
        public void Round_IsIdempotent()
        {
            // Yuvarlanmis bir degeri tekrar yuvarlamak degistirmemeli
            var once = MoneyHelper.Round(12.345m);
            MoneyHelper.Round(once).Should().Be(once);
        }
    }
}
