using System.Security.Cryptography;
using System.Text;

namespace Divisima.Core.Security.Tokens
{
    // ══ GF-1b / K3 (GF1-B3 · GF1-B4) - JETONLAR DB'DE OZET OLARAK DURUR ═══════════════════
    //
    // OLCULEN ONCE-DURUM: `user_sessions.refresh_token` ve `customers.password_reset_token`
    // DB'de DUZ METIN duruyordu. Veritabani okuma yetkisi ya da bir yedek dosyasi, CANLI
    // oturum jetonlarini ve sifre sifirlama jetonlarini DOGRUDAN ele gecirme demekti -
    // ikisi de tek basina HESAP ELE GECIRMEYE yeter.
    //
    // ── NEDEN HEX, BASE64 DEGIL (OLCULDU, D2 karari) ─────────────────────────────────────
    // Dort jeton kolonu da `Turkish_CI_AS` collation'indadir, yani jeton eslesmesi
    // BUYUK/KUCUK HARF DUYARSIZDIR (olculdu: `a` <-> `A` ESIT). base64 ozet secilseydi bu
    // SURERDI: base64'te `a` ve `A` FARKLI SEMBOLLERDIR, CI collation onlari BIRLESTIRIR ->
    // hem etkin entropi duser (~258 -> ~227 bit) hem de jetonun HARF VARYANTI kabul edilir.
    // hex alfabesinde (`0-9a-f`) `A`-`F` zaten AYNI sembol degerine katlanir, yani CI
    // katlanmasi ZARARSIZDIR ve 64 hex karakter = 256 bit KORUNUR.
    //
    // ── NEDEN TUZSUZ / TEK GECIS ─────────────────────────────────────────────────────────
    // Bunlar PAROLA DEGIL, 32 baytlik KRIPTOGRAFIK RASTGELE jetonlardir
    // (`SecureTokenGenerator`). Sozluk saldirisi yuzeyi YOKTUR, dolayisiyla PBKDF2 gibi bir
    // is faktoru GEREKMEZ ve her istekte odenecek maliyet KABUL EDILEMEZ olurdu (refresh
    // yolu sicak yoldur). Ayni gerekceyle depodaki `two_factor_code` da SHA-256 kullaniyor.
    //
    // ── KOLON ADLARI KORUNDU (K-8 karari) ────────────────────────────────────────────────
    // Ozet AYNI kolona YERINDE yazilir; YENI KOLON YOK. Boylece `DenetimGizlilik` sir
    // listesi ve denetim izi pinleri alan adiyla eslesmeye DEVAM EDER - maskeleme
    // EROZYONU olusmaz.
    public static class JetonOzeti
    {
        // 64 karakterlik KUCUK HARF hex. Kolonlar en az 120 karakter oldugu icin RAHAT sigar
        // (olculdu: refresh_token 500, password_reset_token 120).
        public static string Hesapla(string jeton)
        {
            var ozet = SHA256.HashData(Encoding.UTF8.GetBytes(jeton ?? string.Empty));
            return Convert.ToHexString(ozet).ToLowerInvariant();
        }

        // Pinlerin okudugu sozlesme sabiti.
        public const int OzetUzunlugu = 64;
    }
}
