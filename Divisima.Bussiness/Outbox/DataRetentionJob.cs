using Divisima.DataAccess.Abstract;

namespace Divisima.Bussiness.Outbox
{
    // Açıklayıcı yorum: Veri saklama/temizlik işi (Hangfire günlük). Eski pasif oturumları, işlenmiş outbox
    // mesajlarını ve eski güvenlik/denetim loglarını temizler (KVKK/GDPR saklama süreleri + performans).
    public class DataRetentionJob
    {
        private readonly IUserSessionDal _sessionDal;
        private readonly IOutboxMessageDal _outboxDal;
        private readonly ISecurityEventDal _securityEventDal;

        public DataRetentionJob(IUserSessionDal sessionDal, IOutboxMessageDal outboxDal, ISecurityEventDal securityEventDal)
        {
            _sessionDal = sessionDal;
            _outboxDal = outboxDal;
            _securityEventDal = securityEventDal;
        }

        public async Task RunAsync()
        {
            var now = DateTime.Now;
            // PERFORMANS: TOPLU sil - tek SQL DELETE ... WHERE (foreach GetList->tek-tek DeleteAsync N+1 idi:
            // binlerce eski kayıt = binlerce round-trip + hepsini belleğe yükleme). ExecuteDeleteAsync tek sorguda siler.

            // Açıklayıcı yorum: 90 günden eski pasif oturumları sil
            await _sessionDal.DeleteWhereAsync(s => !s.is_active && s.created_at < now.AddDays(-90));

            // Açıklayıcı yorum: 30 günden eski işlenmiş outbox mesajlarını sil
            await _outboxDal.DeleteWhereAsync(m => m.status == 1 && m.created_at < now.AddDays(-30));

            // Açıklayıcı yorum: 1 yıldan eski güvenlik loglarını sil (Critical hariç - onlar saklanır)
            await _securityEventDal.DeleteWhereAsync(e => e.severity != "Critical" && e.created_at < now.AddYears(-1));
        }
    }
}
