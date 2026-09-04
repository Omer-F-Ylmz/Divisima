namespace Divisima.Core.Utilities.Validation
{
    // ══ GIRDI SINIRLARI - TEK MERKEZ (GF-5 / K4) ═══════════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM (AV-2 / SD-7, LAUNCH BLOKER `[VERI-BOZAN]`): misafir yolunda
    // `guest_name` icin UZUNLUK SINIRI YOKTU. 151 karakterlik bir ad HTTP 500 uretiyordu
    // (EF insert-time), ustelik musteri satiri ZATEN YAZILMIS oluyordu -> YETIM MUSTERI
    // (canli ornek: id 179) ve o e-postanin misafir checkout'ta KALICI 409'u. Ayni deger
    // IKI kolona gidiyor: `customers.name` (NVARCHAR(MAX)) ve `addresses.full_name` (150).
    //
    // NEDEN SABIT, NEDEN RuleBuilder DEGIL (merkez karari, GF-5 / D3): ortak bir
    // `TelefonKurali()` / `AdresKurallari()` uzantisi kurmak KAPSAM ACARDI - o uzanti
    // `SellerRegisterRequestValidator`i da kapsardi ve bu dalganin "Seller* enjeksiyon
    // noktalarina 0 SATIR" siniri DELINIRDI. Bu yuzden paylasilan sey KURAL degil DEGERDIR:
    // validator'lar kendi `RuleFor` zincirlerini korur, yalnizca ayni sabitlere BAKARLAR.
    //
    // KOPYA SAYACI (durust kayit): telefon deseni bugun DORT yerde yaziliydi
    // (AddressRequestValidator · CustomerRegisterRequestValidator ·
    // SellerRegisterRequestValidator · GuestCheckoutValidator; ayrisma OLCULDU ve SIFIR).
    // Bu dosya UCUNU tek degere baglar; `SellerRegisterRequestValidator` DOKUNULMAZ oldugu
    // icin KENDI literalini korur. Yani sayac 4 kopya -> 1 sabit + 1 literal olur, 0 DEGIL.
    // Kalan literal BILINEN kalemdir ve Seller modulu acildiginda kapatilir (00a:92).
    //
    // MESAJLAR BURAYA TASINMADI - BILINCLI: `AddressRequestValidator` "Gecerli bir telefon
    // girin." derken `CustomerRegisterRequestValidator` "Gecerli telefon giriniz." diyor.
    // Ikisini esitlemek MUSTERIYE GORUNEN METNI degistirirdi ve bu dalganin kapsami degil
    // (VITRIN-KALAN 3'te kayitli). Paylasilan yalniz DESEN ve UZUNLUKTUR.
    public static class GirdiSinirlari
    {
        // `customers.name` NVARCHAR(MAX) - kolon zorlamiyor, sinir URUN KARARIDIR ve uye
        // kayit ucunun bugunku olcutuyle AYNIDIR (CustomerRegisterRequestValidator name 100).
        // Misafir `guest_name` de buna baglanir: 100 <= 120 (adres validator) <= 150 (kolon),
        // yani her iki yazma hedefiyle de uyumlu. MIGRATION GEREKMEZ.
        public const int MusteriAdi = 100;

        // `addresses.full_name` kolonu 150 KARAKTER (sys.columns'tan olculdu, varlik
        // sinifindan DEGIL). Uye adres ucunun bugunku olcutu 120'dir ve DEGISTIRILMEDI -
        // bu dalga misafir yolunu kapatiyor, uye yolunu SIKILASTIRMIYOR.
        public const int AdresAdSoyad = 120;

        public const int AdresBasligi = 60;    // addresses.title  kolon 60
        public const int Sehir = 50;           // addresses.city   kolon 60
        public const int Ilce = 50;            // addresses.district kolon 60
        public const int AcikAdres = 500;      // addresses.full_address kolon 500 (TAM SINIR)
        public const int PostaKodu = 10;       // addresses.zip_code kolon 20

        // `addresses.phone` / `customers.phone` kolonlari 20 KARAKTER; desendeki {7,20} ust
        // siniri kolonla BIREBIR ayni - bu tesaduf degil, kolon genisligi desenden turetildi.
        public const string TelefonDeseni = @"^[0-9+\s()-]{7,20}$";

        // ══ request_id KAPISI (GF-5 / D2) ══════════════════════════════════════════════════
        //
        // `orders.request_id` NVARCHAR(80). Bugun HICBIR uzunluk kontrolu yok: 81 karakterlik
        // bir deger `guest_name` ile AYNI ailenin insert-time 500'unu uretir.
        //
        // GUID SARTI BILINCLI OLARAK KONULMADI (merkez karari). Olculdu: dolu 122
        // `request_id`in 54'u GUID DEGIL, ve GUID-disi olanin en yenisi GUID olanin en
        // yenisinden DAHA YENI - yani GUID-disi bicim OLU DEGIL, CANLI. Ustelik istemcinin
        // yedek dali (`frontend/api-bridge.js:2283`, `crypto.randomUUID` desteklenmeyen
        // guvenli-olmayan baglam) "co-<zaman>-<8kar>" uretir ve o dal `FrontendDokunmaHedefi`
        // pini ile BILINCLI korunuyor; frontend bu dalgada DOKUNULMAZ. GUID zorunlulugu
        // o tarayicilarda her checkout'u 400 yapardi -> musteri ya odeyemez ya id'yi dusurup
        // MUKERRER SIPARIS yaratirdi. Kapi bu yuzden BICIM degil TASIYICI sinirlar: uzunluk
        // ve karakter sinifi. Iki gercek bicim de (GUID ve "co-...") bu kumeye girer.
        public const int RequestIdEnUzun = 80;

        // Nokta/alt tire/tire GUID'de de "co-..." biciminde de gecer; bosluk, egik cizgi,
        // tirnak ve kontrol karakteri GECMEZ - bunlar log satirini bolebilen ya da baska bir
        // ayristiriciyi yanitabilecek karakterlerdir.
        public const string RequestIdDeseni = @"^[A-Za-z0-9._-]+$";
    }
}
