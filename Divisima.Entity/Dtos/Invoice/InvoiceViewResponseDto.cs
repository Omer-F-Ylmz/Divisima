using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Invoice
{
    // MANTIK-FIX-2R / K2 - GORUNUR FATURANIN VERI SOZLESMESI.
    //
    // OLCULEN ONCE-DURUM: uc, faturayi SIPARIS VERISINDEN YENIDEN HESAPLAYIP hazir HTML
    // donduruyordu (OrderManager.GetInvoiceHtml): sabit /1.20m matrah, sabit "KDV (%20)"
    // etiketi ve SUNUCUDA bicimlenmis para dizgeleri. `invoices` / `invoice_items` tablolari
    // o yolda HIC OKUNMUYORDU (olculdu: uretimde tax_rate okuyan satir YOK). Sonuc:
    //   - karisik oranli sepette ekran YANLIS oran beyan ediyordu (canli: 12 fatura),
    //   - iptal edilmis siparis TAM GORUNUMLU fatura ciziyordu (iptal izi 0),
    //   - faturasiz siparis icin de belge uretiliyordu.
    //
    // ARTIK: yanit KAYITTAN gelir ve YAPILANDIRILMIS HAM DEGER tasir.
    //
    // SAYI BICIMLENMEZ (kultur-sizintisi yasaginin yeni bicimi): bu yanitta ne "1.049,70"
    // ne "1,049.70" bicimli bir para dizgesi bulunur - alanlar ham decimal'dir ve bicimleme
    // ISTEMCIDE dvsLocale ile yapilir. Boylece sunucu tek bir kulture kilitlenmez ve
    // RequestLocalization ACILMAZ (Sprint 8 madde 13'un olcerek reddettigi yol).
    public class InvoiceViewResponseDto : IDto
    {
        // BOS DURUM SOZLESMESI: faturasi olmayan sipariste false doner ve kalemler BOS gelir.
        // Olculdu: 143 siparisin 47'sinin faturasi YOK ve "Fatura Goruntule" butonu KOSULSUZ
        // ciziliyor - eski uc bu durumda da belge uydurup 200 donuyordu.
        public bool has_invoice { get; set; }

        public string? invoice_number { get; set; }
        public DateTime? invoice_created_at { get; set; }

        // IPTAL ISARETI: faturanin KENDI durumu. Eski ekran bunu HIC gostermiyordu -
        // iptal edilmis fatura gecerliden ayirt edilemiyordu.
        public byte? invoice_status { get; set; }
        public bool invoice_is_cancelled { get; set; }

        public string order_number { get; set; }
        public byte order_status { get; set; }
        public bool order_is_cancelled { get; set; }

        public List<InvoiceViewLineDto> items { get; set; } = new();

        // KDV KIRILIMI: tek bir "oran" gosterilmez. Baslik tax_rate artik kalemlerin
        // AGIRLIKLI ORTALAMASI ve ekrana oran olarak cikarsa Turkiye'de var olmayan bir
        // deger (or. %14,16) beyan edilirdi - bu yuzden oran BAZINDA gruplanir.
        public List<InvoiceViewVatGroupDto> vat_breakdown { get; set; } = new();

        public decimal subtotal { get; set; }    // matrah (KDV haric)
        public decimal tax_amount { get; set; }
        public decimal total { get; set; }       // brut (kalemler + kargo)

        // ODEME OZETI - KAYNAK SIPARIS VERISIDIR (D2 karari: invoices krediyi KAYDETMEZ,
        // kredi bir ODEME ARACIDIR ve belge BRUTTUR). Fatura kalemlerinden AYRI bolum.
        public InvoiceViewPaymentDto payment { get; set; } = new();
    }

    public class InvoiceViewLineDto : IDto
    {
        // KARGO SOZLESMESI: invoice_items.product_id NULL ise bu satir kargo bedelidir.
        public bool is_shipping { get; set; }

        // KARGO SATIRINDA NULL BIRAKILIR. Gerekce (E4): kargo etiketi ekranda SOZLUKTEN
        // cizilir; DB'deki ad ekrana HAM basilmaz. Alani bos birakmak bunu bir istemci
        // adabi olmaktan cikarip YAPISAL kilar - istemci basmak istese de elinde deger yok.
        // Urun satirlarinda ise ad, siparis anindaki SNAPSHOT'tir ve gosterilmelidir.
        public string? product_name { get; set; }

        public int quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal line_subtotal { get; set; }
        public decimal vat_rate { get; set; }
        public decimal vat_amount { get; set; }
        public decimal line_total { get; set; }
    }

    public class InvoiceViewVatGroupDto : IDto
    {
        public decimal vat_rate { get; set; }
        public decimal base_amount { get; set; }
        public decimal vat_amount { get; set; }
        public decimal gross_amount { get; set; }
    }

    public class InvoiceViewPaymentDto : IDto
    {
        public decimal order_total { get; set; }
        public decimal store_credit_used { get; set; }
        public decimal remaining { get; set; }
    }
}
