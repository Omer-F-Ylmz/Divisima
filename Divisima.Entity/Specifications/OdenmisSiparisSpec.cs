using System;
using System.Linq.Expressions;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Entity.Entities;

namespace Divisima.Entity.Specifications
{
    // ══ GF-6 / F1 (K4-DAR) - "ODENMIS SIPARIS" YUKLEMININ EF YUZU ══════════════════════════
    //
    // NEDEN BU DOSYA VAR: kural `PaidOrderSpec.IsPaid(byte status, byte paymentType)` ile
    // `Divisima.Core`da yasar - ama Core, `Divisima.Entity`yi GOREMEZ (Entity -> Core; ters yon
    // DONGU olur, olculdu: Core.csproj icinde Entity referansi 0). Dolayisiyla Core'da
    // `Expression<Func<Order,bool>>` YAZILAMAZ ve EF'e cevrilebilir bir yuklem uretilemez.
    //
    // Bu dosya o boslugu kapatir: AYNI kuralin EF'e cevrilebilir bicimi BURADA, TEK YERDE
    // yazilir ve tuketiciler HAZIR YUKLEM alir. Statik metot cagrisi (`IsPaid(...)`) bir
    // `IQueryable` icinde CEVRILEMEZ, bu yuzden kural gomulu bir kosullu ifade olarak durur.
    //
    // IKI BICIMIN AYRISMASI YAPISAL OLARAK ENGELLENDI: `GuvenlikFix6SozlesmeTests`in
    // TAM MATRIS pini, gecerli HER (durum x odeme turu) ciftinde `PaidOrderSpec.IsPaid` ile
    // buradaki yuklemin DERLENMIS halini karsilastirir. Biri degisip digeri kalirsa pin
    // ISIMLI KIRMIZI verir - yani "ayni kuralin ikinci kopyasi" ailesi burada bir SAYAC ile
    // degil, bir TESTLE kapatilmistir.
    //
    // KAPSAM - BILINCLI: bu yuklemi YALNIZ PARA siteleri kullanir (kupon global limiti · kupon
    // kisi-basi limiti · referans odulu). Raporlama siteleri (Dashboard · Merchandising ·
    // Recommendation · Seller) ESKI `PaidStatuses` kuralinda KALDI ve BILINEN kalemdir (GF-7):
    // orada kapida odeme siparisi ciro/siralama/oneri tarafinda hala `Confirmed`da sayilir.
    public static class OdenmisSiparisSpec
    {
        // ══ KUPON LIMITLERI BU YUKLEMI KULLANMAZ - OLCULMUS KARAR (GF-6 / F1) ═════════════
        //
        // Ilk uygulamada kupon global + kisi-basi limitleri de buraya baglanmisti ve OLCULDU:
        // `usage_limit=1` bir kuponu SEKIZ es zamanli COD siparisinin HEPSI aldi. Kok sebep,
        // COD siparisinin Pending DOGMAMASI: "odenmis" olcutunden `Confirmed` cikinca COD icin
        // sayilacak HICBIR durum kalmiyor ve limit YAPISAL OLARAK uygulanamaz hale geliyor.
        // Kupon limiti "para ALINDI MI" degil "kupon hakki hala CANLI MI" sorusudur; bu yuzden
        // o siteler ESKI kuralda birakildi (ilgili yerlerde ADIYLA yazili).
        // Bu dosyanin kapsami: musteriye PARA CIKISI yapan siteler.

        // REFERANS ODULU: "bu musterinin GERCEKTEN odenmis bir siparisi var mi".
        // Burada TAZE BEKLEYEN dali YOKTUR - odul, odenmemis bir siparisle tetiklenmemeli.
        public static Expression<Func<Order, bool>> MusterininOdenmisSiparisiVar(int musteriId) =>
            o => o.customer_id == musteriId
                 && (o.payment_type != PaidOrderSpec.KapidaOdemeTuru
                        ? PaidOrderSpec.PaidStatuses.Contains(o.status)
                        : o.status == PaidOrderSpec.KapidaOdenmisDurum);

        // TAM MATRIS PININ olctugu sey: yukaridaki kosullu ifadenin BELLEK ICI karsiligi.
        // Uc yuklemin UCU DE bu ifadeyi tasir; pin bunu `PaidOrderSpec.IsPaid` ile karsilastirir.
        public static Expression<Func<Order, bool>> YalnizOdenmis() =>
            o => o.payment_type != PaidOrderSpec.KapidaOdemeTuru
                    ? PaidOrderSpec.PaidStatuses.Contains(o.status)
                    : o.status == PaidOrderSpec.KapidaOdenmisDurum;
    }
}
