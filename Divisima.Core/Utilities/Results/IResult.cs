namespace Divisima.Core.Utilities.Results
{
    // Açıklayıcı yorum: İşlem sonucu arayüzü (Cafixo IResult kalıbı).
    public interface IResult
    {
        bool Success { get; }
        string Message { get; }
    }
}
