using System.Net;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Dtos.Coupon;
using Divisima.Entity.Entities;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Aciklayici yorum: Kupon kurallari ve indirim matematigi. ValidateCoupon gercek SQL uzerinde
    // OTORITER siparis sayimi yapiyor (used_count degil, order-count) - bu davranis pinleniyor.
    // CouponManager ctor IMapper istiyor ama ValidateCoupon onu KULLANMIYOR, null gecilir.
    [Trait("Category", "Sql")]
    public class CouponRulesTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaCouponTest";

        private static CouponManager NewManager(DivisimaDbContext ctx) =>
            new CouponManager(new EfCouponDal(ctx), null!, new EfOrderDal(ctx));

        private async Task<Coupon> NewCouponAsync(byte type, decimal value, decimal minAmount = 0m,
            decimal? maxDiscount = null, DateTime? expire = null, int usageLimit = 0, bool firstOrderOnly = false)
        {
            await using var ctx = NewContext();
            var c = new Coupon
            {
                code = ("T" + Guid.NewGuid().ToString("N").Substring(0, 11)).ToUpperInvariant(),
                discount_type = type, value = value, min_amount = minAmount,
                max_discount_amount = maxDiscount, expire_date = expire,
                usage_limit = usageLimit, per_user_limit = 0,
                first_order_only = firstOrderOnly, is_active = true, created_at = DateTime.Now
            };
            ctx.Set<Coupon>().Add(c);
            await ctx.SaveChangesAsync();
            return c;
        }

        private async Task<(HttpStatusCode code, Result result)> ValidateAsync(string code, decimal cartTotal, int customerId)
        {
            await using var ctx = NewContext();
            return await NewManager(ctx).ValidateCoupon(new CouponValidateRequestDto
            {
                code = code, cart_total = cartTotal, customer_id = customerId
            });
        }

        private static decimal Discount(Result r) => ((dynamic)r).Data.discount_amount;
        private static bool FreeShipping(Result r) => ((dynamic)r).Data.free_shipping;

        [Fact]
        public async Task MinAmountAltinda_Reddedilir_UstundeGecer()
        {
            if (Skipped()) return;
            var cust = await NewCustomerAsync();
            var cpn = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 50m, minAmount: 200m);

            var below = await ValidateAsync(cpn.code, 150m, cust.id);
            below.code.Should().Be(HttpStatusCode.BadRequest);
            below.result.Success.Should().BeFalse();
            below.result.Message.Should().NotBeNullOrWhiteSpace("yalniz statu koduna guvenilmez");

            var above = await ValidateAsync(cpn.code, 250m, cust.id);
            above.code.Should().Be(HttpStatusCode.OK, "esik ustunde kupon gecerli olmali (vakum engeli)");
        }

        [Fact]
        public async Task SuresiGecmisKupon_Reddedilir()
        {
            if (Skipped()) return;
            var cust = await NewCustomerAsync();
            var expired = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 50m, expire: DateTime.Now.AddDays(-1));
            var valid = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 50m, expire: DateTime.Now.AddDays(1));

            var r1 = await ValidateAsync(expired.code, 500m, cust.id);
            r1.code.Should().Be(HttpStatusCode.BadRequest);
            r1.result.Success.Should().BeFalse();

            var r2 = await ValidateAsync(valid.code, 500m, cust.id);
            r2.code.Should().Be(HttpStatusCode.OK, "suresi dolmamis kupon gecerli olmali");
        }

        [Fact]
        public async Task FirstOrderOnly_OncekiOdenmisSiparisiOlan_Reddedilir()
        {
            if (Skipped()) return;
            var yeni = await NewCustomerAsync();
            var eski = await NewCustomerAsync();
            await NewOrderAsync(eski.id, 300m, status: (byte)OrderStatusEnum.Delivered);
            var cpn = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 50m, firstOrderOnly: true);

            var ilk = await ValidateAsync(cpn.code, 500m, yeni.id);
            ilk.code.Should().Be(HttpStatusCode.OK, "hic siparisi olmayan musteri kullanabilmeli");

            var tekrar = await ValidateAsync(cpn.code, 500m, eski.id);
            tekrar.code.Should().Be(HttpStatusCode.BadRequest, "odenmis siparisi olan musteri kullanamaz");
            tekrar.result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task UcIndirimTipi_Matematigi_VeMaxDiscountClamp()
        {
            if (Skipped()) return;
            var cust = await NewCustomerAsync();

            var pct = await NewCouponAsync((byte)DiscountTypeEnum.Percentage, 20m);
            var pctRes = await ValidateAsync(pct.code, 1000m, cust.id);
            pctRes.code.Should().Be(HttpStatusCode.OK);
            Discount(pctRes.result).Should().Be(200m, "1000 uzerinden yuzde 20 indirim 200 olmali");

            var capped = await NewCouponAsync((byte)DiscountTypeEnum.Percentage, 20m, maxDiscount: 150m);
            Discount((await ValidateAsync(capped.code, 1000m, cust.id)).result)
                .Should().Be(150m, "max_discount_amount tavani uygulanmali");

            var fix = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 500m);
            Discount((await ValidateAsync(fix.code, 300m, cust.id)).result)
                .Should().Be(300m, "sabit indirim sepet tutarini asamaz");

            var ship = await NewCouponAsync((byte)DiscountTypeEnum.FreeShipping, 0m);
            var shipRes = await ValidateAsync(ship.code, 300m, cust.id);
            FreeShipping(shipRes.result).Should().BeTrue("kargo bedava bayragi set edilmeli");
            Discount(shipRes.result).Should().Be(0m, "kargo kuponunda tutar indirimi olmamali");
        }

        [Fact]
        public async Task UsageLimit_OTORITER_SiparisSayimiyla_Isler()
        {
            if (Skipped()) return;
            var cust = await NewCustomerAsync();
            var cpn = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 50m, usageLimit: 2);

            (await ValidateAsync(cpn.code, 500m, cust.id)).code
                .Should().Be(HttpStatusCode.OK, "limit dolmadan gecerli olmali");

            await NewOrderAsync(cust.id, 500m, status: (byte)OrderStatusEnum.Confirmed, couponCode: cpn.code);
            await NewOrderAsync(cust.id, 500m, status: (byte)OrderStatusEnum.Delivered, couponCode: cpn.code);

            var dolu = await ValidateAsync(cpn.code, 500m, cust.id);
            dolu.code.Should().Be(HttpStatusCode.BadRequest, "usage_limit ODENMIS siparis sayimiyla dolmali");
            dolu.result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task IptalEdilenSiparis_KuponHakkiniGeriVerir_H26DavranisiPinlenir()
        {
            if (Skipped()) return;
            var cust = await NewCustomerAsync();
            var cpn = await NewCouponAsync((byte)DiscountTypeEnum.Fixed, 50m, usageLimit: 1);

            var order = await NewOrderAsync(cust.id, 500m, status: (byte)OrderStatusEnum.Confirmed, couponCode: cpn.code);
            (await ValidateAsync(cpn.code, 500m, cust.id)).code
                .Should().Be(HttpStatusCode.BadRequest, "on kosul: tek hakli kupon dolmali");

            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().FindAsync(order.id);
                o.status = (byte)OrderStatusEnum.Cancelled;
                await ctx.SaveChangesAsync();
            }

            // PINLENEN DAVRANIS (H26): PaidOrderSpec.PaidStatuses icinde Cancelled YOK, dolayisiyla
            // iptal kupon hakkini GERI VERIR. Bilincli tercih; degisirse bu test kirmizi olur ve
            // karar yeniden gorusulur - sessizce kaymaz.
            (await ValidateAsync(cpn.code, 500m, cust.id)).code
                .Should().Be(HttpStatusCode.OK, "iptal sonrasi kupon YENIDEN kullanilabilir olmali");
        }
    }
}
