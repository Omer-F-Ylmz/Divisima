using System.Text.Json;
using Divisima.Core.Security;
using Divisima.Entity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Divisima.DataAccess.Interceptors
{
    // Açıklayıcı yorum: SaveChanges'i yakalayıp değişen entity'ler için AuditLog üretir.
    // AuditLog'un kendisi ve OutboxMessage denetlenmez (sonsuz döngü/gürültü engeli).
    //
    // ═══ FIX-1A / F2 - IKI DEGISIKLIK ══════════════════════════════════════════════════════
    //
    // (1) SIR ALANLARI DENETIM KAYDINA HIC GIRMEZ (DenetimGizlilik.SirAlanlari - TEK KAYNAK).
    //     FAZ 1'de OLCULDU: `changes` icinde `password_hash.old` VE `password_hash.new`
    //     (88'er karakter, FARKLI degerler) + `password_salt` (357 karakter) duruyordu; ayrica
    //     `UserSession.refresh_token` 33 satirda, `CustomerDevice.device_token` 3 satirda.
    //     Yani denetim izi, musterinin gecmis VE guncel kimlik bilgisini saklayan IKINCIL BIR
    //     KIMLIK DEPOSUYDU. Artik degistiyse yalnizca sabit bir isaret yazilir - deger degil,
    //     uzunlugu da ozeti de kirpilmis hali de DEGIL.
    //
    // (2) `changes` YALNIZ GERCEKTEN DEGISEN ALANLARI TASIR.
    //     Eski kod `p.IsModified` filtreliyordu ve NIYETI dogruydu; ama DAL'daki
    //     `EfEntityRepositoryBase.UpdateAsync` -> `Context.Set<T>().Update(entity)` cagriliyor
    //     ve EF'in `Update()`u varligi TUM ALANLARIYLA Modified isaretler. Sonuc: 35 alanlik
    //     TAM-VARLIK payload'i (olculdu: Customer satirlarinda 2286 bayta kadar). Filtre artik
    //     `OriginalValue != CurrentValue` uzerinden - yani DAL'in nasil kaydettiginden BAGIMSIZ.
    //
    // FAZ 6'YA DOKUNULMADI (bilincli): `Added` satirlarindaki negatif `entity_id` (EF gecici
    // anahtari), `Added`in bos `changes`i ve NULL `user_id`'ler BU COMMIT'TE DEGISMEDI.
    // Olculdu ki bu, F3'u BLOKE ETMIYOR: `changes` DOLU olan 397 satirin 397'si de `Modified`
    // ve entity_id'leri POZITIF; `Added` satirlarinin 1226/1226'si NULL `changes` tasiyor.
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly HashSet<string> _ignored = new() { nameof(AuditLog), nameof(OutboxMessage) };

        public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
                         ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var audits = new List<AuditLog>();
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog) continue;
                var typeName = entry.Entity.GetType().Name;
                if (_ignored.Contains(typeName)) continue;
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

                audits.Add(new AuditLog
                {
                    table_name = typeName,
                    entity_id = TryGetId(entry),
                    action = entry.State.ToString(),
                    changes = SerializeChanges(entry),
                    user_id = userId,
                    created_at = DateTime.Now
                });
            }

            // Açıklayıcı yorum: Audit kayıtlarını aynı SaveChanges içinde ekle (tek transaction)
            if (audits.Count > 0)
                context.Set<AuditLog>().AddRange(audits);

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static string TryGetId(EntityEntry entry)
        {
            var key = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            return key?.CurrentValue?.ToString() ?? "";
        }

        // Açıklayıcı yorum: Modified'da yalnız GERÇEKTEN değişen alanları (eski->yeni) JSON'a yaz.
        // Sır alanları hiç serileştirilmez; değiştiyse yalnız sabit işaret yazılır.
        private static string SerializeChanges(EntityEntry entry)
        {
            if (entry.State != EntityState.Modified) return null;

            var changed = new Dictionary<string, object>();
            foreach (var p in entry.Properties)
            {
                if (p.Metadata.IsPrimaryKey()) continue;
                if (!DegerDegistiMi(p.OriginalValue, p.CurrentValue)) continue;

                var ad = p.Metadata.Name;
                if (DenetimGizlilik.SirMi(ad))
                {
                    // "Degisti" izi kalir, DEGER yazilmaz.
                    changed[ad] = new { old = DenetimGizlilik.Isaret, @new = DenetimGizlilik.Isaret };
                    continue;
                }
                changed[ad] = new { old = p.OriginalValue, @new = p.CurrentValue };
            }

            // Hicbir alan gercekten degismediyse gurultulu bir "{}" yerine null yazilir.
            return changed.Count == 0 ? null : JsonSerializer.Serialize(changed);
        }

        // byte[] deger esitligi REFERANS karsilastirmasina duser (row_version gibi alanlar her
        // kayitta "degismis" gorunurdu); dizi icerigi tek tek karsilastirilir.
        private static bool DegerDegistiMi(object eski, object yeni)
        {
            if (ReferenceEquals(eski, yeni)) return false;
            if (eski == null || yeni == null) return true;
            if (eski is byte[] a && yeni is byte[] b) return !a.AsSpan().SequenceEqual(b);
            return !eski.Equals(yeni);
        }
    }
}
