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
    // ═══ GF-5 / K3 - `Added` SATIRLARININ entity_id'si ARTIK GERCEK ANAHTAR ════════════════
    //
    // OLCULEN ONCE-DURUM (AV-2 / SC-5, GF-5 on olcumunde uc kanalda dogrulandi):
    // `Added` denetim satirlarinin entity_id'si EF'in GECICI anahtarini tasiyordu - hepsi
    // NEGATIF. Ureten ifade (LITERAL DEGIL - sayi her kosumda buyur, MK-3):
    //     SELECT COUNT(*) FROM audit_logs WHERE action='Added' AND TRY_CAST(entity_id AS int) < 0;
    // Bu ifade olcum aninda 2984 dondu (toplam 4295 satirin %69,5'i) ve 2984/2984 negatifti.
    // ONCEKI kayitlarda gecen "2970" BAYATTIR - sayi bir LITERAL olarak degil, YALNIZ ureten
    // ifadesiyle anilir.
    //
    // DEGER SADECE BAGLANAMAZ DEGIL, MASIF CAKISIYOR: 2984 satir yalnizca 78 farkli degeri
    // paylasiyor (bant `int.MinValue+1001..+1078`); en sik deger 755 satirda ve 34 FARKLI
    // tabloda geciyor. Yani `(table_name, entity_id)` cifti bile TEKIL DEGIL - "hangi kayda
    // ait" sorusu YAPISAL OLARAK yanitlanamiyordu.
    //
    // NEDEN POST-SAVE, NEDEN "HIC YAZMA, SONRA YAZ" DEGIL: denetim satirlari is satiriyla
    // AYNI SaveChanges icinde ekleniyor (`:73-75`) - bu, "is kaydedildi ama izi yok" durumunu
    // YAPISAL OLARAK imkansiz kilan bir garantidir. Satirlari SavedChanges'e ertelemek o
    // garantiyi FEDA ederdi (ikinci yazma duserse denetim satiri HIC OLMAZDI). Bu yuzden
    // satir AYNI transaction'da yazilir, entity_id ise anahtar GERCEKLESTIKTEN sonra
    // duzeltilir. Kismi basarisizlikta satir BUGUNKU haliyle (gecici id ile) kalir - yani
    // en kotu durum GERILEME DEGIL, mevcut durumla AYNIDIR.
    //
    // KAPSAM SINIRI (durust kayit): `changes` alani `Added` icin HALA NULL kaliyor
    // (`SerializeChanges` :90). Bilincli: doldurmak, KVKK redaksiyon sorgusunun
    // (`AccountManager.cs:398-399`, `changes != null` filtreli) kapsamini bir anda
    // buyuturdu ve entity_id ile BIRLIKTE ele alinmasi gereken ayri bir karardir.
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly HashSet<string> _ignored = new() { nameof(AuditLog), nameof(OutboxMessage) };

        // GF-5 / K3: anahtari GECICI olan girdilerle onlarin denetim satirlarinin eslesmesi.
        // Interceptor SCOPED kayitlidir (`Program.cs:234`), yani bu liste ISTEK BASINADIR -
        // statik olsaydi istekler arasi sizardi.
        private readonly List<(EntityEntry Girdi, AuditLog Satir)> _geciciAnahtarlilar = new();

        // Ikinci `SaveChangesAsync` bu interceptor'i YENIDEN tetikler. Dongu zaten SONLANIR
        // (AuditLog `_ignored` icinde, dolayisiyla ikinci turda uretilecek satir YOKTUR) ama
        // bayrak niyeti ACIK yazar ve bekleyen listenin ikinci turda yeniden islenmesini
        // KESIN olarak engeller.
        private bool _duzeltmeKosuyor;

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

                var satir = new AuditLog
                {
                    table_name = typeName,
                    entity_id = TryGetId(entry),
                    action = entry.State.ToString(),
                    changes = SerializeChanges(entry),
                    user_id = userId,
                    created_at = DateTime.Now
                };
                audits.Add(satir);

                // GF-5 / K3: anahtar SU AN gecici mi? Karar BURADA verilmelidir - `SaveChanges`
                // sonrasinda `IsTemporary` FALSE'a doner ve "bu deger uydurma miydi" sorusu
                // ARTIK SORULAMAZ. Kosul `action == "Added"` DEGIL, `IsTemporary`: olcut
                // DAVRANISIN KENDISI olsun istendi - veritabani tarafinda uretilmeyen bir
                // anahtarla eklenen satir (ornegin id'si elle verilmis) BOSUNA guncellenmesin.
                if (GeciciAnahtarliMi(entry))
                    _geciciAnahtarlilar.Add((entry, satir));
            }

            // Açıklayıcı yorum: Audit kayıtlarını aynı SaveChanges içinde ekle (tek transaction)
            if (audits.Count > 0)
                context.Set<AuditLog>().AddRange(audits);

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // ═══ GF-5 / K3 - ANAHTAR GERCEKLESTIKTEN SONRA entity_id DUZELTILIR ════════════════
        //
        // `SavedChangesAsync` EF'in `AcceptAllChanges` adimindan SONRA atesler; bu noktada
        // veritabaninin urettigi anahtar girdiye ISLENMIS durumdadir, yani `TryGetId` artik
        // GERCEK degeri doner.
        //
        // TRANSACTION DAVRANISI (durust kayit): cagiran bir transaction ACMISSA (UnitOfWork
        // yollari) bu ikinci yazma AYNI transaction'a katilir ve atomiktir. Transaction YOKSA
        // (ornegin `Register` akisindaki duz `AddAsync` cagrilari) ilk `SaveChanges` kendi
        // ortuk transaction'ini COMMIT ETMISTIR ve bu yazma AYRI bir transaction olur.
        // O durumda ikinci yazma duserse denetim satiri gecici id ile KALIR - bugunku
        // davranisin AYNISI, yani gerileme yok.
        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (_duzeltmeKosuyor || _geciciAnahtarlilar.Count == 0 || eventData.Context == null)
                return await base.SavedChangesAsync(eventData, result, cancellationToken);

            _duzeltmeKosuyor = true;
            try
            {
                foreach (var (girdi, satir) in _geciciAnahtarlilar)
                    satir.entity_id = TryGetId(girdi);
                _geciciAnahtarlilar.Clear();

                // Denetim satirlari ZATEN izleniyor (ayni context'e eklendiler), dolayisiyla
                // bu cagri onlar icin UPDATE uretir. Yuk: `Added` tasiyan her birim icin
                // +1 gidis-donus (olculdu: register +4, login +1).
                await eventData.Context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _duzeltmeKosuyor = false;
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        // GF-5 / K3: yazma DUSERSE bekleyen liste TEMIZLENIR. Aksi halde ayni scope'ta yapilan
        // BIR SONRAKI `SaveChanges`, artik gecerli olmayan girdileri isleyip yanlis satirlari
        // guncellemeye calisirdi (misafir telafi yolu tam da boyle bir "duser, sonra yine yazar"
        // akisidir).
        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _geciciAnahtarlilar.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        // GF-5 / K3: `PropertyEntry.IsTemporary` EF Core 8.0.30'da MEVCUTTUR (yuklu derlemede
        // olculdu) - paket yukseltmesi GEREKMEDI.
        private static bool GeciciAnahtarliMi(EntityEntry entry)
        {
            var key = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            return key is { IsTemporary: true };
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
