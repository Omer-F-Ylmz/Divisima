using Divisima.Entity.Dtos.Search;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // ══ GUVENLIK-FIX (G3) - ARAMA TERIMI UZUNLUK SINIRI ═══════════════════════════════════
    //
    // OLCULEN ZARAR: `GET /api/Search/products?query=<4000 karakter>` -> HTTP 500.
    // Sunucu logunda sebep: SqlException 8152 "String or binary data would be truncated".
    // Kok sebep: `SearchManager` metni `p.name.ToLower().Contains(q)` ile ariyor; EF bunu
    // `LIKE N'%' + @p + '%'` desenine cevirir ve `ToLower()` sarmalayicisi kolonun tip
    // eslemesini gizledigi icin parametre VARSAYILAN `nvarchar(4000)` ile baglanir.
    // Desen degeri terim + 2 karakterdir; terim 3998'i asinca desen 4000'i asar ve SQL
    // Server yazmayi reddeder. OLCULDU: 3998 -> 200, 4000 -> 500, 5000 -> 500.
    // Her istek ayrica tam yigin izli bir ERROR satiri yaziyordu (9 istek = 6 ERROR satiri,
    // 66 SQL yigin satiri, 17.655 bayt log) - yani kimliksiz bir log sisirme yuzeyi.
    //
    // NEDEN 200 - keyfi degil, SEMADAN TURETILDI:
    //   products.name  nvarchar(200)   <- aranan EN GENIS kolon
    //   products.brand nvarchar(120)
    // 200 karakterden uzun bir terim, 200 karakterlik bir kolonun ICINDE gecemez; yani
    // boyle bir terim TANIM GEREGI hicbir satiri eslestiremez. Sinir bu yuzden tek bir
    // gercek arama sonucunu bile kaybettirmez. 4000/3998 gibi bir sinir da 500'u kapatirdi
    // ama anlamsiz olurdu: 201..3998 arasi terimler bos sonuc icin tam tablo taratirdi.
    public class ProductSearchRequestValidator : AbstractValidator<ProductSearchRequestDto>
    {
        // Aranan en genis kolonun genisligi (products.name). Sema degisirse burasi da degisir.
        public const int MaksTerimUzunlugu = 200;

        public ProductSearchRequestValidator()
        {
            RuleFor(x => x.query)
                .MaximumLength(MaksTerimUzunlugu)
                .WithMessage($"Arama terimi en fazla {MaksTerimUzunlugu} karakter olabilir.");
        }
    }
}
