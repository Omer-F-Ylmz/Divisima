using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Müşteri DAL. Atomik bakiye metotları TOCTOU race'i önler (eşzamanlı çift-harcama).
    public interface ICustomerDal : IEntityRepository<Customer>
    {
        Task<Customer> GetByEmailAsync(string email);

        // ══ GUVENLIK-FIX (G2) ════════════════════════════════════════════════════════
        // GetByEmailAsync GLOBAL `is_active` filtresine tabidir; askiya alinmis bir hesap
        // ona GORUNMEZ. Kayit yolu bu yuzden "adres bos" sanip INSERT deniyor ve
        // IX_customers_email UNIQUE indeksine takilip HTTP 500 donuyordu (olculdu).
        // Bu metot filtreyi ATLAR - normalizasyon (B1: kultursuz kucultme) TEK YERDE kalsin
        // diye ayri bir DAL metodu, cagri yerinde elle normalize etmek DEGIL.
        Task<Customer> GetByEmailIgnoringFiltersAsync(string email);

        // Açıklayıcı yorum: ATOMİK mağaza kredisi düşümü - tek UPDATE ... WHERE store_credit >= amount.
        // Dönen değer 1 ise başarılı, 0 ise yetersiz bakiye/bulunamadı. Read-modify-write race'i tamamen ortadan kaldırır.
        Task<int> TryDecrementStoreCreditAsync(int customerId, decimal amount);

        // Açıklayıcı yorum: ATOMİK sadakat puanı düşümü (WHERE loyalty_points >= points). 0 = yetersiz.
        Task<int> TryDecrementLoyaltyPointsAsync(int customerId, int points);

        // Açıklayıcı yorum: ATOMİK mağaza kredisi ekleme (hediye kartı bozdurma / puan -> kredi). Her zaman uygulanır.
        Task<int> IncrementStoreCreditAsync(int customerId, decimal amount);
        // Aciklayici yorum: ATOMIK puan ekleme (concurrent kazanimlarda lost update engeli)
        Task<int> IncrementLoyaltyPointsAsync(int customerId, int points);
        // Aciklayici yorum: ATOMIK basarisiz-login artisi - YENI degeri doner (lost-update yok; brute-force kilidi guvenli).
        Task<int> IncrementFailedLoginAsync(int customerId);
        // Aciklayici yorum: ATOMIK hesap kilitle (lockout_end set + sayac sifirla).
        Task LockAccountAsync(int customerId, DateTime until);
        // Aciklayici yorum: ATOMIK login durumu sifirla (basarili giris: sayac 0 + kilit yok + son giris).
        Task ResetLoginStateAsync(int customerId, DateTime lastLogin);

        // ══ GF-1b / K10 (GF1-B10) - SIFIRLAMA JETONUNU ATOMIK TUKET ══════════════════════
        // Jetonu YALNIZCA hala gecerliyken (dogru ozet + suresi dolmamis) harcar ve AYNI
        // ifadede yeni sifreyi yazar. Donen deger ETKILENEN SATIR SAYISIDIR:
        // 1 = bu cagri kazandi · 0 = jeton baska bir istek tarafindan ZATEN tuketildi.
        // Ayni ailedendir: TryDecrementStoreCreditAsync / DeactivateIfActiveAsync.
        Task<int> TryConsumeResetTokenAsync(string tokenOzeti, DateTime simdi, byte[] hash, byte[] salt);
    }
}
