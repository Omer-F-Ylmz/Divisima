using Divisima.Entity.Dtos.Admin;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // ══ GUVENLIK-FIX (G3b) - ADMIN ARAMASINDA AYNI SINIR ══════════════════════════════════
    //
    // Depoda serbest metinli LIKE aramasi TAM IKI yerde: storefront `SearchManager` ve
    // `AdminCustomerManager` (ad + e-posta). Ikisi de ayni kok sebebi tasiyor.
    // OLCULDU: `POST /api/admin/customer/list` govdesinde 4000 karakterlik `search` -> HTTP 500.
    // Bu uc ADMIN korumali oldugu icin onem derecesi dusuk, ama ayni hata sinifi; storefront
    // tarafini kapatip burayi acik birakmak sinirin "sema kaynakli" gerekcesiyle celisirdi.
    //
    // SINIR 200: aranan en genis kolon `customers.email` nvarchar(200) (ad kolonu daha dar).
    public class AdminCustomerFilterValidator : AbstractValidator<AdminCustomerFilterDto>
    {
        public const int MaksTerimUzunlugu = 200;

        public AdminCustomerFilterValidator()
        {
            RuleFor(x => x.search)
                .MaximumLength(MaksTerimUzunlugu)
                .WithMessage($"Arama terimi en fazla {MaksTerimUzunlugu} karakter olabilir.");
        }
    }
}
