namespace Divisima.Bussiness.Events
{
    // SPRINT 8 MADDE 3 - ODEME ONAYLANDI OLAYI (outbox uzerinden islenir).
    //
    // DALGA-2-FIX (B10) - ADI "PaymentConfirmed" AMA TETIK "SIPARIS ONAYLANDI"DIR.
    // Olay Sprint 8'de YALNIZ kart yolundan yaziliyordu; kart disi uc onay yolu (kapida odeme,
    // havale admin onayi, admin durum degisikligi) hicbir mesaj yazmiyordu ve dort yan etkiden
    // UCU o yollarda HIC CALISMIYORDU (fatura calisiyordu - dogrudan cagriliyordu).
    // OLCULDU (dev veritabani): kart siparisleri 10/31/33/34 -> sadakat 1/1; kapida odeme
    // siparisleri 12/13/32 -> sadakat 0/0. Siparis #13 kupon kullandi, coupon_usages 0 satir.
    // Olayin ADI korundu (mevcut outbox satirlarindaki event_type degeri "PaymentConfirmed";
    // yeniden adlandirmak bekleyen mesajlari isleyicisiz birakirdi). Anlami: "siparis odenmis
    // sayilir ve yan etkileri uygulanmalidir".
    //
    // NEDEN OUTBOX: bu olayin tetikledigi dort yan etki (fatura, sadakat, referans odulu, kupon
    // sayaci) onceden commit SONRASI "best-effort" kosuyordu. Patlarlarsa adiyla loglaniyor ve
    // siparis zaman cizelgesine not dusuluyordu - ama HIC YENIDEN DENENMIYORDU. Gecici bir
    // aksaklik (DB kesintisi, saglayici zaman asimi) o yan etkiyi KALICI OLARAK kaybettiriyordu:
    // fatura hic kesilmiyor, sadakat hic verilmiyor, referans odulu hic odenmiyor - ustelik
    // musteri "siparisin onaylandi" goruyor.
    //
    // Olay, odemenin durum gecisiyle AYNI TRANSACTION'da yazilir; yani "odeme Success oldu ama
    // olay kaybedildi" durumu OLUSAMAZ. Islem at-least-once'dir: mesaj birden fazla teslim
    // edilebilir, bu yuzden DORT ADIMIN DA IDEMPOTENT olmasi ZORUNLUDUR (Sprint 8'in on kosullari
    // madde 1 ve madde 2 tam bunun icindi).
    //
    // PAYLOAD NEDEN SIPARIS ID'SINDEN IBARET DEGIL: adimlar musteri id'si, tutar ve kupon kodunu
    // kullaniyor. Bunlari olayin ICINE koymak, isleyicinin siparisi yeniden okumasina gerek
    // birakmaz VE olayin uretildigi ANDAKI degerleri tasir - siparis sonradan degisse bile yan
    // etki dogru veriyle uygulanir (snapshot semantigi).
    public class PaymentConfirmedEvent
    {
        public int order_id { get; set; }
        public int customer_id { get; set; }
        public decimal total_price { get; set; }
        public string? coupon_code { get; set; }

        // DALGA-2-FIX (B10): kupon KULLANIM SATIRINI artik isleyici yaziyor (TEK YAZICI - gerekcesi
        // PaymentConfirmedSideEffects 4. adimda), bu yuzden uygulanan indirim de olayla TASINIR.
        // Snapshot semantigi yukaridakiyle ayni: satir, olayin uretildigi ANDAKI indirimi kaydeder.
        // GERIYE DONUK SINIR (durust kayit): bu alan eklenmeden ONCE yazilmis ve hala Pending duran
        // bir mesaj deserialize edilirse 0 gelir. Bekleyen mesaj omru bir dakikadir (Cron.Minutely);
        // dagitim aninda bekleyen mesaj olmasi beklenmez, ama olursa o TEK satirin indirim degeri
        // 0 kaydedilir - sayac (used_count) yine DOGRU olur, cunku o SATIR SAYISINDAN turetilir.
        public decimal discount_amount { get; set; }
    }
}
