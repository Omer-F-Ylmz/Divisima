using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Oturum DAL. Refresh token ile getirme.
    public interface IUserSessionDal : IEntityRepository<UserSession>
    {
        // Aciklayici yorum: ATOMIK toplu oturum gecersizleme (WHERE customer_id AND is_active). Sifre degisimi/logout/reset'te
        // TEK sorgu ile tum aktif oturumlari kapatir (onceki foreach-UpdateAsync N+1 idi). Dondurdugu = kapatilan oturum sayisi.
        Task<int> InvalidateAllForCustomerAsync(int customerId);

        // AKTIF oturum arar (is_active = true). Logout gibi "yalniz yasayan oturumu kapat" yollari icin.
        Task<UserSession> GetByRefreshTokenAsync(string refreshToken);

        // ══ GUVENLIK-FIX (G1) - DURUM FILTRESIZ ARAMA ════════════════════════════════════
        // GetByRefreshTokenAsync sorgusundaki `&& s.is_active` kosulu, DONDURULMUS bir jetonu
        // "hic var olmamis" jetondan ayirt EDILEMEZ hale getiriyordu: ikisi de NULL donuyordu.
        // Oysa dondurulmus bir jetonun tekrar sunulmasi, mesru istemcinin ASLA yapmadigi bir
        // seydir - refresh token hirsizliginin klasik sinyalidir. Bu metot filtreyi kaldirir
        // ki AuthManager iki durumu ayirip zinciri iptal edebilsin.
        Task<UserSession> GetByRefreshTokenAnyStateAsync(string refreshToken);

        // ══ GF-1b / K4 (GF1-B5) - ATOMIK KAPATMA (CAS) ══════════════════════════════════════
        // Oturumu YALNIZCA hala aktifken kapatir. Donen deger ETKILENEN SATIR SAYISIDIR:
        // 1 = bu cagri kazandi · 0 = baska bir istek onceden kapatti (YARIS KAYBEDILDI).
        // Cagiran 0 gorurse bunu YENIDEN KULLANIM sayar ve zincir iptali yoluna girer.
        Task<int> DeactivateIfActiveAsync(int sessionId);
    }
}
