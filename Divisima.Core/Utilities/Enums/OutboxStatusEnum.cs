namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Outbox mesaj durumu.
    public enum OutboxStatusEnum
    {
        Pending = 0,
        Processed = 1,
        Failed = 2,
        Processing = 3   // atomik claim edildi (isleniyor) - iki processor cift teslim etmez
    }
}
