using System.Globalization;
using Divisima.Core.Utilities.Text;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Divisima.API.Logging
{
    // ══ GLOBAL MASKE NOKTASI (GF-5 / K6) ═══════════════════════════════════════════════════
    //
    // NEDEN FORMATTER, NEDEN ENRICHER DEGIL - OLCULDU (KESIN):
    // `Serilog.Events.LogEvent.Exception` SALT OKUNURDUR - yuklu Serilog 3.1.1 derlemesinde
    // reflection ile olculdu: `CanWrite=False`, arka alan `initonly=True`, ve LogEvent'in
    // public metotlari yalnizca `AddOrUpdateProperty` / `AddPropertyIfAbsent` /
    // `RemovePropertyIfPresent`. Yani bir `ILogEventEnricher` YALNIZCA PROPERTY ekleyip
    // kaldirabilir; istisna METNINE DOKUNAMAZ. Sizan satir tam da `{Exception}` alanindadir.
    // `IDestructuringPolicy` de kapsamaz: onun urunu `LogEventPropertyValue`dur ve `Exception`
    // alani property boru hattindan HIC GECMEZ.
    // Geriye TEK calisan yol kalir: ciktiyi METIN haline getiren katmani sarmak.
    //
    // YENI PAKET GEREKMEDI (`00a:180` - LAUNCH ONCESI yeni bagimlilik EKLENMEZ):
    // `ITextFormatter` ve `MessageTemplateTextFormatter` ZATEN yuklu olan Serilog CEKIRDEGINDE
    // public olarak duruyor; `Serilog.Sinks.File` 5.0.0 ve `Serilog.Sinks.Console` 5.0.1'in
    // ikisi de `ITextFormatter` alan bir asiri yukleme tasiyor. `Serilog.Exceptions` ya da
    // `Serilog.Expressions` GEREKMEDI.
    //
    // BILINEN BEDEL (durust kayit): `Console`un `ITextFormatter` alan asiri yuklemesinde
    // `theme` parametresi YOKTUR - konsol RENK TEMASI kaybolur. Uretimde cikti dosyaya ve
    // stdout'a akiyor, renk bir teshis kanali degil; bedel kabul edildi.
    //
    // K6 TEK BASINA YETMEZ - BU DA OLCULDU: boru hatti kurulsa bile `KanitMaskesi`nin olcutu
    // bugunku PII'yi (ad-soyad, telefon) YAKALAMIYOR. Bu yuzden maske burada `LogMetniMaskesi`
    // uzerinden kosuyor: o sinif SQL Server'in "Truncated value: '...'" bicimini ve EF
    // parametre dokumlerini YAPISAL olarak yakalar, jeton/e-posta icin `KanitMaskesi`ye
    // devreder. `KanitMaskesi`nin kendi olcutu GENISLETILMEDI (`KanitMaskesiTests.cs:42` pini
    // korunuyor - merkez karari D10).
    public sealed class MaskeliFormatter : ITextFormatter
    {
        // Serilog'un KENDI varsayilan sablonlari (yuklu derlemeden okundu) - burada
        // DEGISTIRILMEDI, yalnizca aynen kullaniliyor ki cikti bicimi K6 oncesiyle ayni kalsin.
        public const string DosyaSablonu =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
        public const string KonsolSablonu =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        private readonly MessageTemplateTextFormatter _ic;

        public MaskeliFormatter(string sablon)
        {
            // Kultur INVARIANT: cikti bir MAKINE kaydidir, insan-gorunur bicimlendirme degil
            // (CLAUDE.md 6c - KIMLIK vs GORUNTU). Uygulama tr-TR'ye pinli oldugu icin bu
            // ACIKCA verilmelidir; aksi halde ayni log satiri makineye gore farkli bicimlenir.
            _ic = new MessageTemplateTextFormatter(sablon, CultureInfo.InvariantCulture);
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            // Ic formatter TAM SATIRI uretir (istisna dahil), maske ondan SONRA kosar - yani
            // hangi katmanin yazdigindan BAGIMSIZ olarak her sey tek noktadan gecer.
            var ara = new StringWriter();
            _ic.Format(logEvent, ara);
            output.Write(LogMetniMaskesi.Maskele(ara.ToString()));
        }
    }
}
