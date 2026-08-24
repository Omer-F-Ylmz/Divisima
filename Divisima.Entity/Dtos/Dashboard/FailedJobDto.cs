using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // ══ DALGA C / C4 - ARKA PLAN IS HATALARININ OPERATORE GORUNEN TEK YUZEYI ══════════════
    //
    // OLCULEN ONCE-DURUM: uretimde YEDI recurring is kosuyor (outbox islemcisi, veri saklama,
    // rezervasyon temizligi, terk sepet, dogum gunu, win-back, yorum daveti). Bunlardan biri
    // basarisiz olursa operatorun gorebilecegi HICBIR YER yoktu:
    //   - Hangfire panosu: filtre "authenticated + user_type=1" ister, ama uygulamada TEK
    //     kimlik semasi JwtBearer'dir (AddCookie YOK). Tarayici /hangfire'a giderken
    //     Authorization basligi GONDERMEZ -> IsAuthenticated HER ZAMAN false -> pano HERKESE
    //     KAPALI. Ustelik nginx de o yolu 10.0.0.0/8 ile sinirliyor (ikinci kilit).
    //   - Outbox/is durumunu donen admin ucu: SIFIR (tarandi).
    //   - Serilog dosyasi: sunucu diskinde, kimsenin baktigi bir yuzey degil.
    //
    // NEDEN OUTBOX TABLOSU - OLCUME DAYALI: DataRetentionJob YALNIZCA status=1 (Processed)
    // mesajlari siliyor; status=2 (Failed) olanlar KALICI olarak duruyor. Yani basarisiz arka
    // plan isinin dayanikli kaydi ZATEN veritabaninda - gosterilmesi icin yeni bir depolama ya
    // da yeni bir kimlik yuzeyi (cerez semasi) acmaya gerek yok.
    //
    // PAYLOAD BILINCLI OLARAK DISARIDA: mesaj govdesi e-posta adresi, jeton ve siparis ayrintisi
    // tasiyabilir; operatorun "hangi is, kac denemede, hangi hatayla dustu" sorusuna yanit icin
    // gerekli degil. Hata metni de KanitMaskesi'nden gecirilir (CLAUDE.md bolum 1).
    public class FailedJobDto : IDto
    {
        public int id { get; set; }
        public string event_type { get; set; }
        public int retry_count { get; set; }
        public string? error { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? processed_at { get; set; }
    }
}
