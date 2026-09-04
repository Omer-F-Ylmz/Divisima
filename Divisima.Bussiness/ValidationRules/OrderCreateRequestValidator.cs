using Divisima.Core.Utilities.Validation;
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

            // ══ GF-5 / K4 (D2) - request_id TASIYICI KAPISI ═══════════════════════════════
            //
            // `orders.request_id` NVARCHAR(80) ve bu alani YAZAN IKI YOL VAR: misafir
            // (GuestCheckoutDto) ve UYE (burasi). Kapiyi yalniz misafire koymak, ayni
            // kolonun ayni 500'unu uye yolunda ACIK BIRAKIRDI - SD-7'nin ta kendisi.
            // Kural ve sabitler misafir yoluyla AYNI (GirdiSinirlari); mesajlar da ayni.
            //
            // GUID SARTI YOK - gerekce GirdiSinirlari.RequestIdDeseni'nin basinda: dolu 122
            // degerin 54'u GUID DEGIL ve o bicim CANLI; ayrica frontend'in `crypto.randomUUID`
            // yedegi "co-<zaman>-<8kar>" uretiyor, o dal PINLI ve frontend DOKUNULMAZ.
            RuleFor(o => o.request_id)
                .MaximumLength(GirdiSinirlari.RequestIdEnUzun)
                    .WithMessage($"İstek kimliği en fazla {GirdiSinirlari.RequestIdEnUzun} karakter olabilir.")
                .Matches(GirdiSinirlari.RequestIdDeseni)
                    .WithMessage("İstek kimliği yalnızca harf, rakam, nokta, alt tire ve tire içerebilir.")
                .When(o => !string.IsNullOrWhiteSpace(o.request_id));

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
