using System.Linq.Expressions;
using Divisima.Bussiness.Outbox;
using Divisima.Core.DataAccess;
using Divisima.Core.Entities.Abstract;
using Divisima.Core.Utilities.Dtos;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GF-5 / K9 (SC-12) - OUTBOX SAKLAMA PENCERELERI ═════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM: `DataRetentionJob` YALNIZ `status == 1` (islenmis) satirlari
    // siliyordu. Canli olcum (ureten ifadeleriyle, MK-3):
    //     SELECT COUNT(*) FROM outbox_messages WHERE status = 0;  -> olcumde 301
    //     SELECT DATEDIFF(day, MIN(created_at), GETDATE())
    //       FROM outbox_messages WHERE status = 0;                -> olcumde 13 gun
    // Yani islenmemis satirlar SURESIZ birikiyordu ve mail govdeleri DUZ JETON tasiyor
    // (AuthManager dogrulama/sifirlama yollari). `customers` tablosunda ayni jetonlar
    // GF-1b/K3 ile SHA-256 OZETLI iken outbox HAM tasiyordu - ozetleme bir kanal oteden
    // DELINIYORDU.
    //
    // NEDEN DB'SIZ PIN: olculen sey bir YUKLEMDIR ("hangi satir silinir"), bir SQL kosumu
    // degil. Yuklem yakalanip ORNEK SATIRLARA uygulanarak sinanir; boylece pin hizli,
    // deterministik ve ortam bagimsiz olur. Zaman esigi DateTime.Now'dan turedigi icin
    // ornek satirlar GORELI olarak (gun/dakika cinsinden) kurulur.
    public class GuvenlikFix5SaklamaTests
    {
        // Yalniz DeleteWhereAsync yuklemlerini yakalar; diger uyeler cagrilmaz.
        private class YakalayanDal<T> : IEntityRepository<T> where T : class, IEntity, new()
        {
            public List<Expression<Func<T, bool>>> Yuklemler { get; } = new();

            public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate)
            {
                Yuklemler.Add(predicate);
                return Task.FromResult(0);
            }

            public T Get(Expression<Func<T, bool>> filter) => throw new NotSupportedException();
            public List<T> GetList(Expression<Func<T, bool>> filter = null) => throw new NotSupportedException();
            public void Add(T entity) => throw new NotSupportedException();
            public void Update(T entity) => throw new NotSupportedException();
            public void Delete(T entity) => throw new NotSupportedException();
            public Task<T> GetAsync(Expression<Func<T, bool>> filter) => throw new NotSupportedException();
            public Task<List<T>> GetListAsync(Expression<Func<T, bool>> filter = null) => throw new NotSupportedException();
            public Task<List<T>> GetListNoTrackingAsync(Expression<Func<T, bool>> filter = null) => throw new NotSupportedException();
            public Task<PagedResult<T>> GetPagedAsync(PagingRequestDto paging, Expression<Func<T, bool>> filter = null,
                Expression<Func<T, object>> orderBy = null, bool descending = false) => throw new NotSupportedException();
            public Task<int> CountAsync(Expression<Func<T, bool>> filter = null) => throw new NotSupportedException();
            public Task<bool> AnyAsync(Expression<Func<T, bool>> filter = null) => throw new NotSupportedException();
            public Task AddAsync(T entity) => throw new NotSupportedException();
            public Task UpdateAsync(T entity) => throw new NotSupportedException();
            public Task DeleteAsync(T entity) => throw new NotSupportedException();
            public Task<T> GetIgnoringFiltersAsync(Expression<Func<T, bool>> filter) => throw new NotSupportedException();
            public Task<List<T>> GetListIgnoringFiltersAsync(Expression<Func<T, bool>> filter = null) => throw new NotSupportedException();
        }

        private sealed class SahteOturumDal : YakalayanDal<UserSession>, IUserSessionDal
        {
            public Task<int> InvalidateAllForCustomerAsync(int customerId) => throw new NotSupportedException();
            public Task<UserSession> GetByRefreshTokenAsync(string refreshToken) => throw new NotSupportedException();
            public Task<UserSession> GetByRefreshTokenAnyStateAsync(string refreshToken) => throw new NotSupportedException();
            public Task<int> DeactivateIfActiveAsync(int sessionId) => throw new NotSupportedException();
        }

        private sealed class SahteOutboxDal : YakalayanDal<OutboxMessage>, IOutboxMessageDal
        {
            public Task<List<OutboxMessage>> GetPendingAsync(int take) => throw new NotSupportedException();
            public Task<int> TryClaimAsync(int id) => throw new NotSupportedException();
            public Task<int> ReclaimStaleAsync(DateTime esik) => throw new NotSupportedException();
        }

        private sealed class SahteOlayDal : YakalayanDal<SecurityEvent>, ISecurityEventDal { }

        private static async Task<SahteOutboxDal> OutboxYuklemleriniTopla()
        {
            var outbox = new SahteOutboxDal();
            var job = new DataRetentionJob(new SahteOturumDal(), outbox, new SahteOlayDal());
            await job.RunAsync();
            return outbox;
        }

        [Fact]
        public async Task K9_OUTBOX_ISLENMEMIS_ve_OLU_MESAJLAR_ARTIK_SILINIYOR()
        {
            var outbox = await OutboxYuklemleriniTopla();

            // VAKUM KIRICI: is GERCEKTEN outbox'a IKI yuklem gonderiyor olmali - biri eski
            // (islenmis / 30 gun), biri K9 ile eklenen. Tek yuklem gorursek K9 DUSMUS demektir.
            outbox.Yuklemler.Should().HaveCount(2,
                "outbox icin IKI saklama penceresi olmali: islenmis (30 gun) + islenmemis/olu (K9)");

            var yeni = outbox.Yuklemler[1].Compile();

            // Esik jeton omrunden (sifirlama jetonu 30 dk) + 24 saat marjdan turer.
            var eski = DateTime.Now.AddDays(-13);   // canli olcumdeki en eski islenmemis satirin yasi
            var taze = DateTime.Now.AddMinutes(-5); // islemcinin HALA gondermesi gereken satir

            yeni(new OutboxMessage { status = 0, created_at = eski }).Should().BeTrue(
                "13 gundur gonderilememis bir mail OLUDUR ve icindeki jeton COKTAN gecersizdir");
            yeni(new OutboxMessage { status = 2, created_at = eski }).Should().BeTrue(
                "olu mektup (status 2) da temizlenmeli");

            // AYIRT EDICI - EN KRITIK ASSERT: taze bir islenmemis satir SILINMEZ. Aksi halde
            // K9, outbox islemcisinin kuyrugunu altindan cekerdi (mail HIC gitmezdi).
            yeni(new OutboxMessage { status = 0, created_at = taze }).Should().BeFalse(
                "HENUZ islenmemis TAZE satir SILINMEMELI - islemci onu gonderecek");

            // Kapsam siniri: basariyla gonderilmis satirlar bu pencereye GIRMEZ; onlarin
            // kendi 30 gunluk penceresi var (is/teshis degeri tasirlar).
            yeni(new OutboxMessage { status = 1, created_at = eski }).Should().BeFalse(
                "islenmis satirlar K9 penceresine GIRMEZ - 30 gunluk kendi penceresi var");
        }

        [Fact]
        public async Task K9_ESKI_30_GUNLUK_PENCERE_BOZULMADI()
        {
            var outbox = await OutboxYuklemleriniTopla();
            var eskiPencere = outbox.Yuklemler[0].Compile();

            // BOZDUKLARIM kontrolu: K9 yeni bir yuklem EKLEDI, mevcut olani DEGISTIRMEDI.
            eskiPencere(new OutboxMessage { status = 1, created_at = DateTime.Now.AddDays(-31) })
                .Should().BeTrue("31 gunluk ISLENMIS satir hala siliniyor");
            eskiPencere(new OutboxMessage { status = 1, created_at = DateTime.Now.AddDays(-29) })
                .Should().BeFalse("29 gunluk ISLENMIS satir hala korunuyor");
            eskiPencere(new OutboxMessage { status = 0, created_at = DateTime.Now.AddDays(-31) })
                .Should().BeFalse("eski pencere YALNIZ status=1 satirlarini kapsar");
        }
    }
}
