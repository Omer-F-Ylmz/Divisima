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

        // ══ GF-5 / F4 (C-2) - E-POSTA UZUNLUGU ═════════════════════════════════════════════
        //
        // OLCULEN ACIK: `customers.email` kolonu 200 KARAKTER (sys.columns) ama HICBIR
        // validator'da e-posta uzunluk kurali YOKTU - ne uye ne misafir yolunda ("email" ve
        // "MaximumLength" ayni satirda 0 gecis, uc bagimsiz olcum). 202 karakterlik bir
        // e-posta EF insert-time HTTP 500 uretiyordu; canli olarak yeniden uretildi.
        //
        // Bu, SD-7 (`guest_name` 151 -> 500) ile AYNI AILEDIR ve K4'te GOZDEN KACTI: o turda
        // sinir "ilgili uye validator'unun olcutu" diye turetilmisti, e-postanin ise uye
        // tarafinda da olcutu YOKTU - yani "yok"u kopyalamak bosluğu KORUMUS oldu.
        //
        // DEGER KOLONDAN TURER, URUN KARARI DEGIL: 200 = kolonun kendisi. Daha dar bir sinir
        // (or. RFC 5321'in 254 baytlik yolu) burada UYDURMA olurdu - kolon 200 oldugu surece
        // kirilma noktasi 200'dur. Kolon buyurse sabit onunla birlikte buyur.
        public const int EPosta = 200;         // customers.email kolon 200

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
        // yedek dali (`frontend/api-bridge.js`, `crypto.randomUUID` desteklenmeyen
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

        // ══ ODEME YONTEMI KUMESI (GF-6 / K3 · D4) ══════════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM (AV-3 / T1-B3): `OrderCreateRequestDto.payment_method` bir `byte`
        // ve HICBIR dogrulamasi YOKTU. `OrderManager.PlaceOrder` yalniz IKI degeri ADIYLA
        // taniyor (`== 1` COD · `== 2` havale); TANIMSIZ her deger (3, 7, 99, 255) sessizce
        // "else" dalina, yani ONLINE odemeye dusuyordu. Musteri "kapida odeme" sanip online
        // dala giren bir siparis uretebiliyordu - misafir yolunda AYNI aile GF-1 oncesi
        // kapatilmisti (`GuestOnlyCashOnDelivery`: sessizce COD'a DUSURME YOK).
        //
        // NEDEN BURADA, NEDEN YENI ENUM DEGIL: bu dalganin kapsami `GirdiSinirlari`yi girdi
        // sinirlarinin TEK KAYNAGI olarak aniyor; yeni bir enum dosyasi acmak kapsam disi bir
        // yuzey olurdu. Degerler URETIMDEN TURETILDI, uydurulmadi - `OrderManager`in kendi
        // dallari (`isCod`/`isBankTransfer`) ve `Order.payment_type` yorumu okundu.
        public const byte OdemeOnline = 0;
        public const byte OdemeKapida = 1;   // COD
        public const byte OdemeHavale = 2;   // Havale/EFT - Pending kalir, admin manuel onaylar

        public static readonly byte[] GecerliOdemeYontemleri = { OdemeOnline, OdemeKapida, OdemeHavale };

        // ══ CSV ICE-AKTARIM SINIRLARI (GF-6 / K7 · D7) ═════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM (AV-3 / T2-1, T2-2, T2-6 · ayrica F-3 IMPORT-FIX kaydi):
        //   (a) SATIR SINIRI YOKTU - tum dosya bellege okunuyor, satir basina EN AZ bir
        //       `GetAsync` sorgusu kosuyor; 1 MB'lik bir CSV on binlerce gidis-donus demek.
        //   (b) DOSYA TURU SORULMUYORDU - `IFormFile` ne olursa olsun metin gibi okunuyordu.
        //   (c) FORMUL ENJEKSIYONU: `=`, `+`, `-`, `@` ile baslayan hucre, urun adi olarak
        //       kaydedilip admin panelinden Excel'e disari aktarildiginda FORMUL olarak
        //       calisir (CSV injection / DDE). Kaynak dosyada da hedef dosyada da metindir -
        //       tehlike ELEKTRONIK TABLONUN yorumlamasindadir, bu yuzden GIRISTE reddedilir.
        //
        // SATIR SINIRI 5000 - URETIMDEN TURETILDI, UYDURULMADI: bugunku katalog `955` urun
        // (kurgu MAX kaydi) ve bir urun birden cok BEDEN satiri tasir; 5000 satir, mevcut
        // katalogun tamamini tek dosyada yeniden yuklemeye YETER ve hala tavan olur.
        public const int CsvSatirEnCok = 5000;

        // 5 MB - ust sinir DOSYA duzeyinde de bagimsiz olarak konur: satir sayimi ancak dosya
        // BELLEGE OKUNDUKTAN sonra yapilabilir, yani satir siniri tek basina bellek tuketimini
        // sinirlamaz. Iki kapi FARKLI seyi korur ve ikisi de gereklidir.
        public const long CsvDosyaEnBuyukBayt = 5L * 1024L * 1024L;

        // `products.name` kolonu 200, `products.brand` 120 (DivisimaDbContext'ten OKUNDU,
        // varlik sinifindan DEGIL). `ProductAddRequestValidator` ayni degerleri kullaniyor;
        // CSV yolu ise HICBIR uzunluk sormuyordu - 201 karakterlik bir ad dongunun ORTASINDA
        // insert-time 500 uretirdi (SD-7 ailesi).
        public const int UrunAdi = 200;
        public const int UrunMarkasi = 120;

        // Elektronik tablonun formul baslangici sayacagi karakterler.
        public static readonly char[] FormulBaslangiclari = { '=', '+', '-', '@' };
    }
}
