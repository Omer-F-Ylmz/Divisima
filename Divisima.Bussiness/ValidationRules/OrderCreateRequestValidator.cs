using Divisima.Entity.Dtos.Order;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Sipariş oluşturma validasyonu.
    public class OrderCreateRequestValidator : AbstractValidator<OrderCreateRequestDto>
    {
        public OrderCreateRequestValidator()
        {
            RuleFor(o => o.customer_id).GreaterThan(0).WithMessage("Geçerli müşteri gerekli.");

            // ══ GUVENLIK-FIX (G9) - NEGATIF MAGAZA KREDISI GIRISTE REDDEDILIR ═════════════
            // OLCULEN ONCE-DURUM: `use_store_credit = -1000` -> HTTP 201 (siparis OLUSTU).
            // ZARAR YOKTU: OrderManager degeri `> 0 ? deger : 0` ile yutuyordu ve bakiye
            // DEGISMEDI (olculdu: 100.00 -> 100.00). Yani bu bir ACIK degil, DOGRULAMA
            // BOSLUGUDUR: anlamsiz girdi sessizce kabul ediliyor, istemci hatasi gorunmez
            // kaliyor ve tek savunma manager'daki tek satirlik ifade oluyor.
            RuleFor(o => o.use_store_credit).GreaterThanOrEqualTo(0)
                .WithMessage("Kullanılacak mağaza kredisi negatif olamaz.");

            RuleFor(o => o.items).NotEmpty().WithMessage("Sepet boş olamaz.");
            RuleForEach(o => o.items).ChildRules(item =>
            {
                item.RuleFor(i => i.product_id).GreaterThan(0).WithMessage("Geçerli ürün gerekli.");
                item.RuleFor(i => i.quantity).GreaterThan(0).WithMessage("Adet en az 1 olmalı.")
                    .LessThanOrEqualTo(100).WithMessage("Tek üründen en fazla 100 adet.");
                item.RuleFor(i => i.size).NotEmpty().WithMessage("Beden seçilmeli.");
            });
        }
    }
}
