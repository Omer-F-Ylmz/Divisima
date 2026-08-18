namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Güvenlik olaylarını kaydeder + kritikse admin'e anlık bildirim/mail tetikler.
    public interface ISecurityEventService
    {
        Task LogAsync(string eventType, string severity, int? customerId, string? ip, string? userAgent, string? detail);
    }
}
