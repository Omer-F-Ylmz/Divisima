using Asp.Versioning;

namespace Divisima.API.Versioning
{
    // ══ SPRINT 8 MADDE 9 - WEBHOOK YOLUNDA "X-Api-Version" BASLIGI YOK SAYILIR ═════════════
    //
    // OLCULEN ENGEL (22 Agustos 2026, GERCEK Iyzico bildirimi, public tunel uzerinden):
    // Iyzico her webhook bildiriminde "X-Api-Version: V1" yolluyor. Program.cs'teki
    // HeaderApiVersionReader("X-Api-Version") bu degeri ayristiramiyor ve istek CONTROLLER'A
    // HIC ULASMADAN bos govdeli 400 ile reddediliyordu (log: "Request contained the API
    // version 'V1', which is not valid"). Canli zarar: siparis #33 - para Iyzico'da SUCCESS,
    // bizde Pending; "callback kayboldu" senaryosunda TEK kurtarma yolu calismiyordu.
    //
    // ══ NEDEN ATTRIBUTE DEGIL - UC KEZ OLCULDU, TAHMIN DEGIL ═══════════════════════════════
    //   [ApiVersionNeutral] ACTION duzeyinde      -> HALA 400 (bos govde)
    //   [ApiVersionNeutral] CONTROLLER duzeyinde  -> HALA 400 (bos govde)
    //   Yolu boru hattinin basinda temizleyen bir app.Use(...) middleware'i -> HALA 400
    // Sebep OLCULDU: uygulama app.UseRouting()'i ACIKCA cagirmiyor, bu yuzden yonlendirme
    // (ve onunla birlikte ApiVersionMatcherPolicy) boru hattinin BASINA ekleniyor - kullanici
    // middleware'lerinden ONCE kosuyor. Yani bir middleware basligi "cok gec" siliyor.
    // Ustelik reddi yapan katman ENDPOINT'IN VERSIYON-NOTRLUGUNE BAKMIYOR: okuyucu basligi
    // ayristiramayinca istek versiyon-notr uclarda da dusuyor.
    // Geriye tek dogru yer kaliyor: OKUYUCUNUN KENDISI.
    //
    // ══ KAPSAM BILEREK DAR ════════════════════════════════════════════════════════════════
    // Yalniz TEK yol muaf. "Ayristirilamayan surumu her yerde yok say" demek TUM API'nin
    // davranisini degistirirdi; diger uclari BIZIM kendi istemcimiz cagiriyor ve orada bozuk
    // bir surum basligi GORULMESI gereken bir hatadir. Webhook ise adresini bir UCUNCU TARAFIN
    // cagirdigi tek uc - o taraf bizim surum sozlesmemizi bilmiyor ve bilmek zorunda degil.
    // PINLI (WebhookContractTests): ayni baslikla /api/category/getlist HALA 400 verir.
    //
    // Muafiyet SURUM SECIMINI BOZMAZ: bu yolda hicbir okuyucu deger uretmedigi icin
    // AssumeDefaultVersionWhenUnspecified devreye girer ve varsayilan surum (1.0) secilir.
    //
    // DEFTERE YAZILDI (SUPHELI): kirilganlik webhook'a ozel DEGIL - "X-Api-Version" basligini
    // ayristirilamaz bir degerle gonderen HERHANGI bir istemci, hangi uca giderse gitsin
    // blanket 400 alir. Genel cozum (tolere eden okuyucu ya da acik hata mesaji) AYRI bir
    // karardir ve bu sprint'e girmedi.
    public sealed class WebhookExemptHeaderApiVersionReader : IApiVersionReader
    {
        private const string HeaderName = "X-Api-Version";
        private const string MuafYol = "/api/payment/webhook";

        private readonly IApiVersionReader _inner = new HeaderApiVersionReader(HeaderName);

        public IReadOnlyList<string> Read(HttpRequest request)
            => request.Path.StartsWithSegments(MuafYol, StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>()
                : _inner.Read(request);

        // Swagger/ApiExplorer tarafi DEGISMEZ: basligin belgelenmesi muafiyetten bagimsiz.
        public void AddParameters(IApiVersionParameterDescriptionContext context)
            => _inner.AddParameters(context);
    }
}
