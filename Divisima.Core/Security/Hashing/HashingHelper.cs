using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Divisima.Core.Security.Hashing
{
    // ══ GF-1 / K6 (C-4) - PBKDF2'YE GECIS, SURUM DEGERIN ZARFINDAN TURER ══════════════════
    //
    // OLCULEN ONCE-DURUM: sifreler HMAC-SHA512 ile TEK GECISTE ozetleniyordu - iterasyon YOK,
    // is faktoru YOK. HMAC-SHA512 bir MAC'tir, PAROLA TUREVI DEGILDIR: GPU ile saniyede
    // milyarlarca deneme yapilabilir. Depoda adaptif bir KDF paketi de yoktu (50 paket
    // tarandi: BCrypt/Argon2/PBKDF2/PasswordHasher -> 0).
    //
    // ── SURUM NEDEN KOLON DEGIL, ZARF (merkez karari) ─────────────────────────────────────
    // Ilk tarif `customers`a `password_hash_version` kolonu ekliyordu. OLCUM bunu curuttu:
    // bu yardimci PAYLASILANDIR - `SellerAuthManager` de (kayit + login + kukla dogrulama)
    // ayni metotlari cagiriyor ve `sellers` AYRI bir tablodur. Surum yalniz `customers`a
    // konsaydi satici yolu geri dusecek surum bilgisini BULAMAZ ve MEVCUT TUM SATICI
    // GIRISLERI KIRILIRDI - ustelik Seller GF-1'de DOKUNULMAZ.
    // (Bugun `sellers` 0 SATIR oldugu icin fiili zarar 0'di; kirilan sey SOZLESMEYDI.)
    // Zarf cozumu her iki tabloda da calisir ve HICBIR migration gerektirmez.
    //
    // ── ZARF (uzunluga gore AYRISTIRILIR, tahmine gerek YOK) ──────────────────────────────
    //   v1 (eski):  hash = 64 bayt (HMAC-SHA512 ciktisi)   · salt = 128 bayt (HMAC anahtari)
    //   v2 (yeni):  hash = 69 bayt = [0x02] + [iterasyon, 4 bayt big-endian] + [64 bayt PBKDF2]
    //                                                      · salt = 16 bayt
    // 64 != 69 oldugu icin ayrim UZUNLUKLA KESINDIR; onek baytina "sansa" kalinmaz (v1'in ilk
    // bayti da 0x02 olabilir - 1/256 - ama uzunlugu 69 OLAMAZ). Iterasyon sayisi zarfin ICINDE
    // tasindigi icin ileride artirilsa bile ESKI v2 hash'leri dogrulanabilir kalir.
    //
    // ── KOLONLAR DEGISMIYOR ───────────────────────────────────────────────────────────────
    // `password_hash` / `password_salt` iki tabloda da `varbinary(max)` (olculdu) - 69 ve 16
    // bayt oraya SORUNSUZ sigar. Sema DEGISMEDI.
    public static class HashingHelper
    {
        // v2 zarf sabitleri. `Iterasyon` merkez tarafindan verildi (100k).
        private const byte SurumV2 = 0x02;
        private const int Iterasyon = 100_000;
        private const int TuzUzunluguV2 = 16;
        private const int AnahtarUzunlugu = 64;      // PBKDF2-SHA512 ciktisi
        private const int OnekUzunlugu = 1 + 4;      // surum bayti + iterasyon (big-endian)
        private const int ZarfUzunluguV2 = OnekUzunlugu + AnahtarUzunlugu;   // 69
        private const int HashUzunluguV1 = 64;
        private const int TuzUzunluguV1 = 128;

        // Açıklayıcı yorum: Şifreden hash + salt üret (kayıt anında). ARTIK HER ZAMAN v2 uretir.
        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            passwordSalt = RandomNumberGenerator.GetBytes(TuzUzunluguV2);
            var anahtar = Turet(password, passwordSalt, Iterasyon);

            passwordHash = new byte[ZarfUzunluguV2];
            passwordHash[0] = SurumV2;
            BinaryPrimitives.WriteInt32BigEndian(passwordHash.AsSpan(1, 4), Iterasyon);
            anahtar.CopyTo(passwordHash.AsSpan(OnekUzunlugu));
        }

        // Açıklayıcı yorum: Girilen şifre kayıtlı hash+salt ile eşleşiyor mu (login anında).
        //
        // ── ZAMANLAMA: HER YOL AYNI MALIYETI ODER (merkez sarti) ─────────────────────────
        // Bu metot HANGI dala girerse girsin TAM BIR PBKDF2 turetmesi kosar. Gerekce olculdu
        // ve GUVENLIK-FIX-2/#19'un kapattigi oracle sinifina aittir: v2'ye gecmis bir hesap
        // 100k iterasyon oderken v1'de kalmis GERCEK bir hesap mikrosaniyede yanitlansaydi,
        // HIZLI YANIT "bu hesap eski/kayitli" bilgisini ELE VERIRDI. Ayni sey kukla
        // dogrulama yolu icin de gecerli: `AuthManager` kayitsiz adreste bu metodu SAHTE
        // deger ile cagiriyor - o cagri da ayni maliyeti odemelidir.
        // Bedeli: v1 dogrulamasi bir PBKDF2 turetmesi kadar YAVASLAR. Bilincli takas.
        public static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            var hash = passwordHash ?? Array.Empty<byte>();
            var tuz = passwordSalt ?? Array.Empty<byte>();

            // ── 0-BAYT KAYITLAR: GUVENLI RED, PATLAMA YOK ────────────────────────────────
            // KVKK anonimlestirmesi hash/salt alanlarini `Array.Empty<byte>()` yapiyor
            // (`AccountManager` - olculdu: 6 satir, hepsi is_active=0). Bu satirlar HICBIR
            // sifreyle eslesMEMELI ve istisna FIRLATMAMALI. Maliyet yine de odenir ki bu
            // hesaplar zamanlamadan ayirt edilemesin.
            if (hash.Length == 0 || tuz.Length == 0)
            {
                _ = Turet(password, KuklaTuz, Iterasyon);
                return false;
            }

            // ── v2: PBKDF2 zarfi ─────────────────────────────────────────────────────────
            if (hash.Length == ZarfUzunluguV2 && hash[0] == SurumV2)
            {
                var iterasyon = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(1, 4));
                if (iterasyon <= 0) return false;
                var hesaplanan = Turet(password, tuz, iterasyon);
                return CryptographicOperations.FixedTimeEquals(hesaplanan, hash.AsSpan(OnekUzunlugu));
            }

            // ── v1: HMAC-SHA512 (eski kayitlar BAYT-DEGISMEZ dogrulanmaya devam eder) ────
            // Kayit satirina DOKUNULMAZ: bu metot yalniz DOGRULAR. Yeniden yazma karari
            // cagirana aittir (bkz. `SurumGuncelGerekiyorMu`).
            using var hmac = new HMACSHA512(tuz);
            var v1Hesaplanan = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            var esit = CryptographicOperations.FixedTimeEquals(v1Hesaplanan, hash);

            // SABIT MALIYET: v1 yolu da v2 kadar surer (yukaridaki gerekce).
            _ = Turet(password, KuklaTuz, Iterasyon);
            return esit;
        }

        // Cagiran, dogrulama BASARILI olduktan sonra bunu sorar: kayit hala eski bicimdeyse
        // sessizce v2'ye yeniden yazilir. Kararin BURADA degil cagirida olmasi bilincli -
        // bu yardimci veritabani BILMEZ.
        public static bool SurumGuncelGerekiyorMu(byte[] passwordHash)
        {
            var hash = passwordHash ?? Array.Empty<byte>();
            if (hash.Length == 0) return false;             // anonimlestirilmis kayit - DOKUNMA
            return !(hash.Length == ZarfUzunluguV2 && hash[0] == SurumV2);
        }

        // Zamanlama esitleyicisinin kullandigi sabit tuz. DEGERI SIR DEGILDIR: hicbir seyi
        // korumaz, yalnizca ayni miktarda IS yapilmasini saglar.
        private static readonly byte[] KuklaTuz = new byte[TuzUzunluguV2];

        private static byte[] Turet(string password, byte[] tuz, int iterasyon) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password ?? string.Empty),
                tuz,
                iterasyon,
                HashAlgorithmName.SHA512,
                AnahtarUzunlugu);

        // Pinlerin okudugu sozlesme sabitleri (test yalniz OKUR, degistirmez).
        public static int BeklenenV1HashUzunlugu => HashUzunluguV1;
        public static int BeklenenV1TuzUzunlugu => TuzUzunluguV1;
        public static int BeklenenV2HashUzunlugu => ZarfUzunluguV2;
        public static int BeklenenV2TuzUzunlugu => TuzUzunluguV2;
        public static int BeklenenIterasyon => Iterasyon;
    }
}
