using System.Text.RegularExpressions;

namespace Divisima.Core.Utilities.Text
{
    // ══ LOG METNI MASKESI - CERCEVE ISTISNALARI ICIN (GF-5 / K6, merkez karari D10) ════════
    //
    // NEDEN AYRI BIR SINIF, NEDEN `KanitMaskesi` GENISLETILMEDI (merkez karari, GF-5 / D10):
    // `KanitMaskesi`nin olcutu "uzunluk >= 16 + en az bir rakam + en az bir KUCUK HARF"tir ve
    // bu olcut PINLIDIR: `KanitMaskesiTests.cs` siparis numarasinin ("DVS20260823-…", rakam
    // VAR ama kucuk harf YOK) DOKUNULMADAN gectigini ACIKCA pinler. Olcutu genisletmek o pini
    // kirardi ve teshis degeri tasiyan dizgeleri de yutmaya baslardi. Bu yuzden buradaki
    // kurallar AYRI durur ve `KanitMaskesi`ye YALNIZCA devreder.
    //
    // NE ICIN VAR - OLCULEN OLGU: sizan satirlari UYGULAMA KODU YAZMIYOR. `ExceptionMiddleware`
    // istisnayi ZATEN `KanitMaskesi`den geciriyordu; buna ragmen ayni PII log dosyasinda
    // MASKESIZ duruyordu. Kanit: `divisima-20260904.log` icinde ayni olayin UC dump'i var
    // (EF Core'un kendi logger'i · damgasiz ikinci dump · ExceptionMiddleware) ve sizan
    // parcalarinin md5'i UCUNDE DE AYNI (`a90fcf18d894f9dd`), maske eki o satirlarda 0 kez
    // geciyor. Yani cagri-yeri maskesi YAPISAL OLARAK yetmiyor: EF Core'un ve SQL Server'in
    // urettigi metin uygulama kodundan GECMIYOR.
    //
    // Deger `EnableSensitiveDataLogging`den DE gelmiyor - o bayrak tum depoda 0 kez geciyor
    // (olculdu). Metin, SQL Server'in 2628 numarali hata mesajinin ICINDEDIR; yani bayragi
    // kapali tutmak bu sizintiyi ENGELLEMEZ.
    public static class LogMetniMaskesi
    {
        private const int GorunurOnEk = 8;
        private const string Ek = "…";

        // SQL Server 2628: "String or binary data would be truncated in table 'X', column 'Y'.
        // Truncated value: 'DEGER'." - DEGER kullanicinin girdisidir (canli ornekte
        // `customers.phone` ve `addresses.full_name`). Tablo ve kolon adi GORUNUR KALIR:
        // teshis icin gereken sey odur, PII olan yalnizca DEGERDIR.
        private static readonly Regex KirpilanDeger =
            new(@"(Truncated value:\s*')([^']*)(')", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // EF Core parametre dokumu: `@p0='deger'`, `@__email_0='deger'` (ve `(Size = ...)` eki).
        // Bugun `Executed DbCommand` satirlari Warning override'i sayesinde YAZILMIYOR, ama
        // Error seviyesindeki istisna metinleri parametre tasiyabiliyor - kapi ILERI DONUK.
        private static readonly Regex EfParametresi =
            new(@"(@[A-Za-z_][A-Za-z0-9_]*\s*=\s*')([^']*)(')", RegexOptions.Compiled);

        // Bu metin, sizintili dump'in HEMEN ARDINDAN geliyor ve tek basina PII tasimaz;
        // yalniz kayit icin anilir - AYIKLANMAZ.
        //   "The statement has been terminated."

        public static string? Maskele(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return metin;

            // ONCE dar/yapisal kurallar (tirnak icindeki DEGER), SONRA genel jeton/e-posta
            // olcutu. Sira ONEMLI: genel olcut once kosarsa tirnak icindeki degeri parcalara
            // bolup bir kismini birakabilirdi.
            var cikti = KirpilanDeger.Replace(metin, m => m.Groups[1].Value + Kirp(m.Groups[2].Value) + m.Groups[3].Value);
            cikti = EfParametresi.Replace(cikti, m => m.Groups[1].Value + Kirp(m.Groups[2].Value) + m.Groups[3].Value);

            // Jeton ve e-posta icin TEK KAYNAK `KanitMaskesi` - kural burada KOPYALANMAZ.
            return KanitMaskesi.Maskele(cikti);
        }

        // Kirpma bicimi `KanitMaskesi` ile AYNI: ilk 8 karakter + tek nokta ucu. Kisa degerler
        // TAMAMEN gider - 8 karakterin altindaki bir ad/telefon zaten onekiyle taninabilirdi.
        private static string Kirp(string deger)
        {
            if (string.IsNullOrEmpty(deger)) return deger;
            return deger.Length <= GorunurOnEk ? Ek : deger.Substring(0, GorunurOnEk) + Ek;
        }
    }
}
