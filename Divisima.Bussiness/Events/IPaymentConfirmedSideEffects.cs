namespace Divisima.Bussiness.Events
{
    // SPRINT 8 MADDE 3 - ODEME ONAYI YAN ETKILERI (outbox isleyicisi bunu cagirir).
    //
    // Dort adim TEK bir arayuzun arkasinda: fatura, sadakat puani, referans odulu, kupon sayaci.
    // `OutboxProcessor`'a dort ayri servis enjekte etmek yerine tek bagimlilik veriliyor -
    // isleyici ince kalir ve yan etkilerin sirasi/mantigi TEK yerde durur.
    //
    // SOZLESME: metot AT-LEAST-ONCE cagrilir. Ayni siparis icin ikinci kez cagrilmasi FAZLA
    // ETKI URETMEMELIDIR. Dort adimin idempotentlik dayanaklari:
    //   fatura         -> "bu siparis icin fatura zaten var" kontrolu + durum guard'i (madde 2)
    //   sadakat        -> UX_loyalty_transactions_order_earn filtreli UNIQUE indeksi (Sprint 6)
    //   referans odulu -> UX_store_credit_referee_reward filtreli UNIQUE indeksi (madde 3)
    //   kupon sayaci   -> coupon_usages satirlarindan TURETME (madde 1) + UNIQUE indeks
    //
    // HATA SOZLESMESI: adimlardan biri patlarsa ISTISNA FIRLATILIR. Yutulmaz - cunku outbox'in
    // yeniden deneme mekanizmasi ancak istisna gorurse calisir. "Best-effort" davranis tam da
    // bu tasarimda BIRAKILDI.
    public interface IPaymentConfirmedSideEffects
    {
        Task ApplyAsync(PaymentConfirmedEvent evt);
    }
}
