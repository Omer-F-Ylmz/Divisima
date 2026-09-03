using Divisima.Entity.Dtos.Product;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // ══ GF-3 / K13 (GF-2a devri) - URUN GUNCELLEME DOGRULAMASI ═════════════════════════════
    //
    // OLCULEN ONCE-DURUM (GF-2a'da bulundu, GF-3'te dort kanaldan dogrulandi: sinif adi ·
    // AbstractValidator<> tip parametresi · DTO'dan geri arama · DataAnnotations taramasi):
    // bu sinif YOKTU. `ProductManager.Update` gelen DTO'yu `_mapper.Map(dto, product)` ile
    // TAM-VARLIK maplenip `Context.Update()` ile yaziyor ve elle dogruladigi TEK sey fiyat.
    // Sonuc: `color_hex` alanina <= 9 karakterlik HER dizge 200 ile DB'ye giriyordu
    // (kolon `nvarchar(9) NOT NULL`), 9'dan uzun olan ise 500 uretiyordu. Tek savunma
    // ISTEMCIDEYDI (`admin.html` + GF-2a'nin `guvenliRenk`i) - yani bir API istemcisi
    // dogrudan kirli deger yazabiliyordu.
    //
    // KURALLAR "Add" ILE BIREBIR AYNI: regex `ProductAddRequestValidator`den KOPYALANDI
    // (ezberden yazilmadi). Ikisinin AYRISMASI bu depoda kayitli bir kusur sinifidir -
    // `CategoryUpdateRequestValidator` ayni asimetriyi kendi yorumunda anlatiyor ve bu,
    // sinifin UCUNCU duzeltilisi.
    //
    // KAYIT OTOMATIK: `Program.cs:267-268` `AddValidatorsFromAssembly(Divisima.Bussiness)`
    // derlemedeki TUM validator'lari tarar - `Program.cs` ve Autofac modulu DOKUNULMADI.
    // 400 zarfi da degismez (`InvalidModelStateResponseFactory` zaten `ErrorResult` uretiyor).
    //
    // MEVCUT VERI MAHSUR KALMAZ - OLCULDU: dev veritabanindaki 35 urunun 35'i gecerli 6-hex
    // (`COLLATE Latin1_General_BIN2` ile sayildi, NEG kontrol `#ZZZZZZ` -> 0), 3/4 haneli
    // kisa hex 0 satir. Yani sikilastirma bugun hicbir satiri guncellenemez hale getirmiyor.
    //
    // NOT (GF-2a/K4 ile iliski): istemci tarafi render allowlist'i {3,4,6,8} ile DAHA GENIS
    // kalir. Gerekcesi "eski kirli kayitlar" DEGIL (olculdu: 0) - CSV yolu ve dogrudan API
    // cagrilari ILERIDE kisa hex sokabilecegi icin render tarafi savunmada kalir.
    public class ProductUpdateRequestValidator : AbstractValidator<ProductUpdateRequestDto>
    {
        public ProductUpdateRequestValidator()
        {
            RuleFor(p => p.id).GreaterThan(0).WithMessage("Ürün kimliği gerekli.");
            RuleFor(p => p.name).NotEmpty().WithMessage("Ürün adı boş olamaz.").MaximumLength(200);
            RuleFor(p => p.brand).NotEmpty().WithMessage("Marka boş olamaz.").MaximumLength(120);
            RuleFor(p => p.category_id).GreaterThan(0).WithMessage("Kategori gerekli.");
            RuleFor(p => p.price).GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalı.");
            RuleFor(p => p.color_hex).Matches("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")
                .When(p => !string.IsNullOrEmpty(p.color_hex))
                .WithMessage("Renk geçerli hex formatında olmalı (#RRGGBB).");
            RuleFor(p => p.old_price).GreaterThan(p => p.price)
                .When(p => p.old_price.HasValue)
                .WithMessage("İndirim öncesi fiyat, satış fiyatından büyük olmalı.");
        }
    }
}
