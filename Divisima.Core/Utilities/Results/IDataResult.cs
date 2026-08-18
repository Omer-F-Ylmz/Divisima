namespace Divisima.Core.Utilities.Results
{
    // Açıklayıcı yorum: Veri taşıyan işlem sonucu arayüzü.
    public interface IDataResult<T> : IResult
    {
        T Data { get; }
    }
}
