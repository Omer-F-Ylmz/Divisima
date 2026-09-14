using System.Globalization;
using Divisima.Bussiness.Jobs;
using Hangfire;

namespace Divisima.API.Services
{
    // MON-1 / D1: alarm imleci Hangfire'in SQL deposundaki bir hash alaninda durur.
    //
    // NEDEN BURASI: sema zaten dagitimda kurulu (`HangFire.Hash`), yeni kolon/migration GEREKMEZ
    // ve deger uygulama yeniden baslayinca KAYBOLMAZ. Redis secenegi elendi: yeniden baslayan
    // Redis imleci siler ve ilk tur tabani yeniden kurarken araya dusen olaylari yutar.
    // Hash'e son kullanma suresi VERILMEZ - Hangfire'in suresi dolan kayit temizligi yalniz
    // ExpireAt tasiyan satirlari siler.
    public class HangfireAlarmImleci : IAlarmImleci
    {
        public const string HashAnahtari = "divisima:kritik-olay-alarm";
        public const string Alan = "son_id";

        private readonly JobStorage _depo;

        public HangfireAlarmImleci(JobStorage depo) => _depo = depo;

        public Task<int?> OkuAsync()
        {
            using var baglanti = _depo.GetConnection();
            var hash = baglanti.GetAllEntriesFromHash(HashAnahtari);
            // KIMLIK dizgesi: kultursuz ayristirma (uygulama tr-TR'ye pinli, CLAUDE.md 6c).
            int? deger = hash != null && hash.TryGetValue(Alan, out var ham)
                         && int.TryParse(ham, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id
                : null;
            return Task.FromResult(deger);
        }

        public Task YazAsync(int sonId)
        {
            using var baglanti = _depo.GetConnection();
            baglanti.SetRangeInHash(HashAnahtari, new[]
            {
                new KeyValuePair<string, string>(Alan, sonId.ToString(CultureInfo.InvariantCulture)),
            });
            return Task.CompletedTask;
        }
    }
}
