using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Divisima.Core.Security.Tokens
{
    // ══ LF-5 / D1 - E-POSTA DOGRULAMA KODU (6 HANE) ═════════════════════════════════════════
    //
    // NEDEN AYRI SERVIS: kodu URETEN ve OZETLEYEN mantik TEK YERDE durmali. `AuthManager`
    // bunu uc ayri yerde kullanir (register · resend · verify) ve uc kopya, bu deponun yedi
    // kez bedelini odedigi "ayni kuralin ikinci kopyasi" ailesinin tam ornegi olurdu.
    //
    // ══ NEDEN HMAC, DUZ SHA-256 DEGIL (merkez karari, olculmus gerekce) ════════════════════
    //
    // Alti haneli bir kodun TUM UZAYI 1.000.000'dur. Duz `SHA-256(kod)` saklanirsa, DB sizan
    // bir saldirgan bir milyon ozeti SIRADAN bir makinede SANIYEDEN KISA surede uretip
    // eslestirir - yani ozet, kodu PRATIKTE KORUMAZ. (Bu, eski 43 karakterlik rastgele jeton
    // icin GECERLI DEGILDI: onun uzayi kaba kuvvetle taranamazdi. Kisa koda gecmek, ozetleme
    // olcutunu de degistirmeyi ZORUNLU kilar.)
    //
    // HMAC-SHA256 + SUNUCU TARAFI BIBER bunu kapatir: biber `Encryption:Key`ten TURETILIR ve
    // VERITABANINDA DURMAZ. DB tek basina sizarsa saldirganin elinde anahtarsiz bir HMAC
    // kalir; sozlugu onceden hesaplayamaz.
    //
    // BIBER AYRISTIRILIR (`DIVISIMA_EMAIL_VERIFY_v1`): ayni ana anahtar baska yerlerde de
    // kullaniliyor ve bir baglamin ozeti baska bir baglamda YENIDEN KULLANILAMAMALIDIR.
    //
    // GERIYE KALAN SINIR (durust kayit): anahtar VE veritabani BIRLIKTE sizarsa koruma biter.
    // O noktada zaten alan sifrelemesi de dusmus olur; kabul edilmis sinirdir.
    public interface IDogrulamaKoduServisi
    {
        // Kriptografik rastgele 6 haneli kod. Dondurulen deger DUZ METINDIR ve YALNIZ
        // e-postaya gider - cagiran onu SAKLAMAZ, `Ozetle` sonucunu saklar.
        string Uret();

        // Duz kodun saklanabilir ozeti (HMAC-SHA256, buyuk harf hex).
        string Ozetle(string kod);

        // Sabit-zamanli karsilastirma. Duz `==` ozet dizgelerinde zamanlama farki sizdirabilir;
        // olcut ucuz oldugu icin dogru olan bastan yazilir.
        bool Eslesiyor(string girilenKod, string? saklananOzet);
    }

    public sealed class DogrulamaKoduServisi : IDogrulamaKoduServisi
    {
        private readonly byte[] _biber;

        public DogrulamaKoduServisi(IConfiguration config)
        {
            // `AesEncryptionProvider` ile AYNI kaynak ve AYNI dusus davranisi: uretimde
            // `Encryption:Key` bos olamaz (Program.cs fail-fast'i acilista durdurur), yerelde
            // sabit bir gelistirme anahtarina duser. Ikinci bir yapilandirma anahtari
            // ACILMADI - operatore yeni bir zorunlu alan eklemek, unutuldugunda SESSIZ bir
            // zayiflama uretirdi.
            var b64 = config["Encryption:Key"] ?? "";
            var anaAnahtar = b64.Length > 0
                ? Convert.FromBase64String(b64)
                : SHA256.HashData(Encoding.UTF8.GetBytes("DIVISIMA_DEV_ENCRYPTION_KEY"));

            // BAGLAM AYRIMI: ana anahtar dogrudan kullanilmaz, bu amaca ozel turetilir.
            _biber = HMACSHA256.HashData(anaAnahtar, Encoding.UTF8.GetBytes("DIVISIMA_EMAIL_VERIFY_v1"));
        }

        // `GetInt32(0, 1_000_000)` REDDET-VE-YENIDEN-DENE kullanir, yani dagilim DUZGUNDUR.
        // `% 1000000` ile modulo almak kucuk bir SAPMA uretirdi; ucuz oldugu icin dogrusu
        // secildi. `D6` bastaki sifirlari korur - "007321" gecerli bir koddur.
        public string Uret() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        public string Ozetle(string kod) =>
            Convert.ToHexString(HMACSHA256.HashData(_biber, Encoding.UTF8.GetBytes(kod ?? "")));

        public bool Eslesiyor(string girilenKod, string? saklananOzet)
        {
            if (string.IsNullOrWhiteSpace(girilenKod) || string.IsNullOrWhiteSpace(saklananOzet))
                return false;

            var beklenen = Encoding.UTF8.GetBytes(saklananOzet);
            var gelen = Encoding.UTF8.GetBytes(Ozetle(girilenKod.Trim()));
            return CryptographicOperations.FixedTimeEquals(beklenen, gelen);
        }
    }
}
