using System.Text.Json;
using Divisima.Entity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Divisima.DataAccess.Interceptors
{
    // Açıklayıcı yorum: SaveChanges'i yakalayıp değişen entity'ler için AuditLog üretir.
    // AuditLog'un kendisi ve OutboxMessage denetlenmez (sonsuz döngü/gürültü engeli).
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

        // Açıklayıcı yorum: Modified'da sadece değişen alanları (eski->yeni) JSON'a yaz
        private static string SerializeChanges(EntityEntry entry)
        {
            if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => new { old = p.OriginalValue, @new = p.CurrentValue });
                return JsonSerializer.Serialize(changed);
            }
            return null;
        }
    }
}

