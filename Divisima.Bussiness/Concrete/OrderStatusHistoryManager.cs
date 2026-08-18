using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Sipariş durum geçmişi iş kuralları.
    public class OrderStatusHistoryManager : IOrderStatusHistoryService
    {
        private readonly IOrderStatusHistoryDal _historyDal;
        private readonly IOrderDal _orderDal;

        public OrderStatusHistoryManager(IOrderStatusHistoryDal historyDal, IOrderDal orderDal)
        {
            _historyDal = historyDal;
            _orderDal = orderDal;
        }

        public async Task RecordAsync(int orderId, byte status, string note)
        {
            await _historyDal.AddAsync(new OrderStatusHistory
            {
                order_id = orderId,
                status = status,
                note = note,
                created_at = DateTime.Now
            });
        }

        public async Task<(HttpStatusCode, Result)> GetTimeline(int orderId, int customerId)
        {
            // Açıklayıcı yorum: IDOR koruması - sipariş sahibinin dışındakiler "bulunamadı" alır
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null || order.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorDataResult<List<OrderStatusHistoryDto>>(Messages.OrderNotFound));

            var history = await _historyDal.GetListNoTrackingAsync(h => h.order_id == orderId);
            var timeline = history
                .OrderBy(h => h.created_at)
                .Select(h => new OrderStatusHistoryDto
                {
                    status = h.status,
                    status_name = ((OrderStatusEnum)h.status).ToString(),
                    note = h.note,
                    created_at = h.created_at
                })
                .ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<OrderStatusHistoryDto>>(timeline));
        }
    }
}
