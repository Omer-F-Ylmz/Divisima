namespace Divisima.Core.Security
{
    // ══ SIFRE POLITIKASI - TEK MERKEZ ══════════════════════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM (SUPHELI #21, LAUNCH-FIX Dalga A / A2'de bulundu): sifre belirlenen
    // DORT yolda DORT ayri davranis vardi ve en gevsek olan, en kolay ulasilan yoldu:
    //
    //   POST /api/auth/register           8 + buyuk + kucuk + rakam   (CustomerRegisterRequestValidator)
    //   POST /api/seller/auth/register    8 + buyuk + kucuk + rakam   (AYNI KURALIN BIREBIR KOPYASI)
    //   POST /api/account/change-password YALNIZCA >= 6, karmasiklik YOK
    //   POST /api/auth/reset-password     HICBIR KONTROL YOK - dogrudan hash'leniyordu
    //
    // URETIMDEKI ANLAMI: "Sifremi unuttum" ile gelen biri, KAYITTA reddedilecek bir sifreyi
    // (ornegin "abc") belirleyebiliyordu. Yani politika, ATLATILMASI EN KOLAY yoldan
    // uygulanmiyordu - bir politika ancak EN ZAYIF girisi kadar gucludur. A2 bu akisi arayuze
    // BAGLADIGI icin kapi her musteriye acildi ve kalem SUPHELI'den DUZELTME'ye yukseltildi.
    //
    // KARAR (kullanici): taban BUGUNKU EN KATI kural olsun; dort giris de BURADAN turesin.
    // Bu, change-password icin bir SIKILASTIRMADIR (6 -> 8 + karmasiklik) ve bilinclidir:
    // ayni hesabin sifresini belirleyen iki yolun farkli guc istemesi savunulabilir degil.
    //
    // MESAJ SOZLESMESI: Dogrula() IHLAL EDILEN ILK kuralin ozel mesajini doner (null = gecerli).
    // Genel bir "sifre gecersiz" mesaji SECILMEDI - kullanici hangi kurali cignedigini
    // bilmezse deneme yanilmaya duser. Bu mesajlar KAYIT ucunda bugune kadar zaten
    // gosteriliyordu; degisen tek sey artik DORT ucta da ayni olmalari.
    public static class SifrePolitikasi
    {
        public const int AsgariUzunluk = 8;

        public const string BosMesaji = "Şifre boş olamaz.";
        public const string UzunlukMesaji = "Şifre en az 8 karakter olmalı.";
        public const string BuyukHarfMesaji = "Şifre en az bir büyük harf içermeli.";
        public const string KucukHarfMesaji = "Şifre en az bir küçük harf içermeli.";
        public const string RakamMesaji = "Şifre en az bir rakam içermeli.";

        /// <summary>
        /// Gecerliyse null, degilse IHLAL EDILEN ILK kuralin mesaji.
        /// </summary>
        public static string? Dogrula(string? sifre)
        {
            if (string.IsNullOrWhiteSpace(sifre)) return BosMesaji;
            if (sifre.Length < AsgariUzunluk) return UzunlukMesaji;

            bool buyuk = false, kucuk = false, rakam = false;
            foreach (var c in sifre)
            {
                // ASCII DEGIL, KULTURSUZ DE DEGIL - char.IsUpper/IsLower Unicode'a bakar ve
                // kultur BAGIMSIZDIR (CLAUDE.md bolum 6c: kulturlu casing YALNIZ kimlik
                // dizgesinde yasak; burada bir DONUSTURME degil SINIFLANDIRMA yapiliyor).
                // Turkce "Ş" buyuk harf, "ş" kucuk harf olarak DOGRU sayilir - regex tabanli
                // "[A-Z]" kontrolu bunlari GORMEZDI ve Turkce sifre kullanan musteriyi
                // gereksizce zorlardi. Kayit ucundaki eski regex bu yuzden birebir taklit
                // EDILMEDI; kural GEVSEMEDI, kapsami GENISLEDI.
                if (char.IsUpper(c)) buyuk = true;
                else if (char.IsLower(c)) kucuk = true;
                else if (char.IsDigit(c)) rakam = true;
            }

            if (!buyuk) return BuyukHarfMesaji;
            if (!kucuk) return KucukHarfMesaji;
            if (!rakam) return RakamMesaji;
            return null;
        }

        public static bool Gecerli(string? sifre) => Dogrula(sifre) is null;
    }
}
