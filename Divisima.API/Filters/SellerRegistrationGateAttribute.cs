using Divisima.Core.Utilities.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Divisima.API.Filters
{
    // ══ GUVENLIK-FIX (G7) - KAPI, DOGRULAMADAN ONCE ═══════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM (satici kaydi KAPALIYKEN):
    //   eksik govde   -> HTTP 400 "The email field is required."   <-- KAPI HIC GORULMEDEN
    //   gecerli govde -> HTTP 403 "Satici basvurulari su anda kapali."
    // Yani kayit KAPALIYKEN bile anonim bir caginan, DTO'nun zorunlu alanlarini tek tek
    // ogrenebiliyordu. Sizan sey kucuk (alan adlari), ama kapali bir kapinin arkasindaki
    // sozlesmeyi anlatmasi icin hicbir sebep yok.
    //
    // KOK SEBEP SIRALAMAYDI, KOD DEGIL: kapi kontrolu action GOVDESININ ILK SATIRINDAYDI ve
    // dogru yazilmisti - ama [ApiController]'in otomatik 400'unu ureten `ModelStateInvalidFilter`
    // Order = -2000 ile action'dan ONCE kosuyor ve istegi orada kesiyor. Bu yuzden kapi
    // action govdesinde DEGIL, ondan da ONCE kosan bir filtrede olmali.
    //
    // Order = -2001: ModelStateInvalidFilter'dan (-2000) TAM BIR ONCE. Daha kucuk bir sayi
    // secmek de calisirdi; -2001 "tam onune geciyorum" niyetini acikca yaziyor.
    //
    // KAPI KAPALI DEGILSE bu filtre HICBIR SEY yapmaz - dogrulama ve action normal isler.
    public sealed class SellerRegistrationGateAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
    {
        public const string ConfigKey = "Seller:RegistrationEnabled";

        public int Order => -2001;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var enabled = bool.TryParse(config[ConfigKey], out var e) && e;
            if (!enabled)
            {
                context.Result = new ObjectResult(new ErrorResult("Satıcı başvuruları şu anda kapalı."))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
            await next();
        }
    }
}
