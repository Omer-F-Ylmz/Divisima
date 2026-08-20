using System.Linq;
using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Abstract;
using Microsoft.Extensions.Configuration;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: İYS/ETK kapısının tek implementasyonu (bkz. IMarketingGate).
    //
    // BULUNAN DURUM: kayıt sırasında pazarlama rızası ConsentRecord olarak YAZILIYORDU
    // (AuthManager.Register - kabul de ret de saklanıyor) ama HİÇBİR gönderim yolu bu kaydı
    // OKUMUYORDU. Yani rıza kaydı yalnız yazılıp duruyordu. AbandonedCartManager ayrıca
    // notify_email tercihini de kontrol etmiyordu - yalnız is_active bakıyordu.
    public class MarketingGate : IMarketingGate
    {
        private const string MarketingConsentType = "marketing";

        private readonly IConfiguration _config;
        private readonly ICustomerDal _customerDal;
        private readonly IConsentRecordDal _consentDal;

        public MarketingGate(IConfiguration config, ICustomerDal customerDal, IConsentRecordDal consentDal)
        {
            _config = config;
            _customerDal = customerDal;
            _consentDal = consentDal;
        }

        // Açıklayıcı yorum: VARSAYILAN FALSE. Anahtar yoksa/bozuksa kapalı kabul edilir -
        // fail-closed: yanlışlıkla açık kalmaktansa yanlışlıkla kapalı kalsın.
        public bool Enabled => bool.TryParse(_config["Marketing:Enabled"], out var v) && v;

        public async Task<bool> CanSendToCustomerAsync(int customerId)
        {
            if (!Enabled) return false;

            var customer = await _customerDal.GetAsync(c => c.id == customerId);
            if (customer == null) return false;            // pasif/silinmiş müşteri (global filtre) - gönderme
            if (!customer.notify_email) return false;      // kullanıcı tercihi

            return await HasMarketingConsentAsync(customerId);
        }

        public async Task<bool> CanSendToEmailAsync(string email)
        {
            if (!Enabled) return false;
            if (string.IsNullOrWhiteSpace(email)) return false;

            var customer = await _customerDal.GetByEmailAsync(email);
            // Müşteri kaydı yoksa: kişi bu ürün için ADRESİNİ KENDİ girip abone olmuş demektir;
            // abonelik açık rızadır, tek belirleyici bayraktır.
            if (customer == null) return true;

            if (!customer.notify_email) return false;
            return await HasMarketingConsentAsync(customer.id);
        }

        // EN GÜNCEL rıza kaydı belirleyicidir: kişi sonradan reddetmiş olabilir, o yüzden
        // "granted = true kaydı var mı" DEĞİL, "en son kayıt ne diyor" sorulur.
        private async Task<bool> HasMarketingConsentAsync(int customerId)
        {
            var records = await _consentDal.GetListNoTrackingAsync(r =>
                r.customer_id == customerId && r.consent_type == MarketingConsentType);
            if (records.Count == 0) return false;          // rıza kaydı yoksa gönderilmez

            var latest = records
                .OrderByDescending(r => r.created_at)
                .ThenByDescending(r => r.id)               // aynı saniyede iki kayıt varsa son yazılan
                .First();
            return latest.granted;
        }
    }
}
