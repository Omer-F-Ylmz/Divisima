namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: TİCARİ ELEKTRONİK İLETİ (İYS/ETK) KAPISI - TEK karar noktası.
    //
    // Neden tek nokta: pazarlama maili dört ayrı yerden gidiyor (terk-sepet, doğum günü,
    // win-back, yorum daveti, fiyat düşüşü). Kural her birine ayrı ayrı yazılsaydı zamanla
    // ayrışır ve biri "izinsiz gönderen" hâline gelirdi. Karar burada verilir, çağıranlar
    // yalnız sorar.
    //
    // KAPSAM İÇİ (pazarlama): terk-sepet, doğum günü teklifi, win-back, yorum daveti, fiyat düşüşü.
    // KAPSAM DIŞI (işlemsel - bu kapıdan GEÇMEZ): e-posta doğrulama, parola sıfırlama, sipariş
    // onayı, kargo bildirimi, fatura, iade, stokta-var bildirimi. Bunlar müşterinin kendi
    // işlemine ait bildirimlerdir; rıza bayrağıyla susturulamaz.
    public interface IMarketingGate
    {
        // Marketing:Enabled bayrağı. Varsayılan FALSE - launch'ta kapalı başlar, İYS kaydı
        // tamamlanınca açılır.
        bool Enabled { get; }

        // Müşteri kimliği bilinen gönderimler (terk-sepet, doğum günü, win-back, yorum daveti).
        // Bayrak + en güncel "marketing" rıza kaydı (granted) + notify_email tercihi.
        Task<bool> CanSendToCustomerAsync(int customerId);

        // E-posta bazlı abonelikler (fiyat düşüşü). Müşteri kaydı varsa aynı kurallar uygulanır;
        // yoksa aboneliğin kendisi açık rızadır ve yalnız bayrak belirleyicidir.
        Task<bool> CanSendToEmailAsync(string email);
    }
}
