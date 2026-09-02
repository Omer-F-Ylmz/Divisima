using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Oturum DAL implementasyonu.
    public class EfUserSessionDal : EfEntityRepositoryBase<UserSession, DivisimaDbContext>, IUserSessionDal
    {
        public EfUserSessionDal(DivisimaDbContext context) : base(context)
        {
        }

        // ══ GF-1b / K3 - ARAMA OZET UZERINDEN YAPILIR ═══════════════════════════════════════
        //
        // `refresh_token` kolonu artik DUZ METIN DEGIL, SHA-256 hex OZET tutuyor. Cagiranlar
        // istemciden gelen DUZ jetonu verir; ozetleme BURADA, TEK YERDE yapilir.
        // NEDEN DAL'DA: iki arama metodu ve gelecekteki her cagiran icin TEK KAYNAK - cagri
        // yerinde ozetlemek "ayni kuralin ikinci kopyasi" ailesini acardi (bu depoda YEDI KEZ
        // bedeli odendi). Cagiran DUZ jetondan baska bir sey BILMEK ZORUNDA DEGIL.
        //
        // MEVCUT DUZ METIN SATIRLAR (merkez karari): geriye donuk ozetleme YAPILMADI; o
        // satirlar ozet aramasiyla ESLESMEZ ve fiilen OLU oturuma doner. Launch oncesi kabul.
        private static string Ozet(string duzJeton) => Divisima.Core.Security.Tokens.JetonOzeti.Hesapla(duzJeton);

        // Açıklayıcı yorum: Refresh token ile AKTIF oturum
        public async Task<UserSession> GetByRefreshTokenAsync(string refreshToken)
        {
            var ozet = Ozet(refreshToken);
            return await Context.Set<UserSession>()
                .FirstOrDefaultAsync(s => s.refresh_token == ozet && s.is_active);
        }

        // ══ GUVENLIK-FIX (G1) - DURUM FILTRESIZ ARAMA ═══════════════════════════════════════
        // Yukaridaki metot `is_active` filtreledigi icin DONDURULMUS bir jeton da "bulunamadi"
        // olarak donuyordu; yani yeniden kullanim sinyali DAL'da kayboluyordu. Bu metot satiri
        // durumundan BAGIMSIZ getirir - karar (401 mi, zincir iptali mi) is katmaninindir.
        // NoTracking DEGIL: cagiran ayni context icinde InvalidateAllForCustomerAsync cagiriyor.
        public async Task<UserSession> GetByRefreshTokenAnyStateAsync(string refreshToken)
        {
            var ozet = Ozet(refreshToken);
            return await Context.Set<UserSession>()
                .FirstOrDefaultAsync(s => s.refresh_token == ozet);
        }

        // GF-1b / K4: ATOMIK kapatma. `WHERE is_active = 1` sartini VERITABANINA birakir -
        // check-then-act yarisi OLUSMAZ. Donen sayi 1 ise bu cagri kazandi, 0 ise oturum
        // ZATEN kapatilmisti (yani ayni jeton bir kez daha sunuldu).
        public async Task<int> DeactivateIfActiveAsync(int sessionId)
        {
            return await Context.Set<UserSession>()
                .Where(s => s.id == sessionId && s.is_active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.is_active, false));
        }

        // Aciklayici yorum: TEK atomik UPDATE - tum aktif oturumlari kapatir (foreach yerine).
        public async Task<int> InvalidateAllForCustomerAsync(int customerId)
        {
            return await Context.Set<UserSession>()
                .Where(us => us.customer_id == customerId && us.is_active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(us => us.is_active, false));
        }
    }
}
