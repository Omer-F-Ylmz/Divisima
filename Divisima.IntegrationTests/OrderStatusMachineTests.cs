using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Sipariş durum makinesi testleri - geçersiz geçişlerin (Cancelled->Shipped vb.) engellendiği.
    public class OrderStatusMachineTests
    {
        [Theory]
        [InlineData(OrderStatusEnum.Pending, OrderStatusEnum.Confirmed)]
        [InlineData(OrderStatusEnum.Pending, OrderStatusEnum.Cancelled)]
        [InlineData(OrderStatusEnum.Confirmed, OrderStatusEnum.Preparing)]
        [InlineData(OrderStatusEnum.Confirmed, OrderStatusEnum.Cancelled)]
        [InlineData(OrderStatusEnum.Preparing, OrderStatusEnum.Shipped)]
        [InlineData(OrderStatusEnum.Shipped, OrderStatusEnum.Delivered)]
        public void ValidTransitions_Allowed(OrderStatusEnum from, OrderStatusEnum to)
        {
            OrderStatusMachine.IsValidTransition(from, to).Should().BeTrue();
        }

        [Theory]
        [InlineData(OrderStatusEnum.Cancelled, OrderStatusEnum.Shipped)]    // iptal -> kargo (yasak)
        [InlineData(OrderStatusEnum.Cancelled, OrderStatusEnum.Confirmed)]  // iptal geri alınamaz
        [InlineData(OrderStatusEnum.Delivered, OrderStatusEnum.Pending)]    // teslim -> beklemede (geri)
        [InlineData(OrderStatusEnum.Delivered, OrderStatusEnum.Shipped)]    // teslim -> kargo (geri)
        [InlineData(OrderStatusEnum.Pending, OrderStatusEnum.Delivered)]    // ödeme atlanamaz
        [InlineData(OrderStatusEnum.Pending, OrderStatusEnum.Shipped)]      // hazırlık atlanamaz
        [InlineData(OrderStatusEnum.Shipped, OrderStatusEnum.Cancelled)]    // kargodaki iptal edilemez
        [InlineData(OrderStatusEnum.Confirmed, OrderStatusEnum.Delivered)]  // aşama atlama
        public void InvalidTransitions_Blocked(OrderStatusEnum from, OrderStatusEnum to)
        {
            OrderStatusMachine.IsValidTransition(from, to).Should().BeFalse();
        }

        [Theory]
        [InlineData(OrderStatusEnum.Pending)]
        [InlineData(OrderStatusEnum.Delivered)]
        [InlineData(OrderStatusEnum.Cancelled)]
        public void SameStatus_IsIdempotent(OrderStatusEnum status)
        {
            // Aynı duruma "geçiş" serbest (idempotent güncelleme)
            OrderStatusMachine.IsValidTransition(status, status).Should().BeTrue();
        }
    }
}
