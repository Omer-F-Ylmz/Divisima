namespace Divisima.Core.Utilities.Results
{
    // Açıklayıcı yorum: Başarılı sonuç (veri yok).
    public class SuccessResult : Result
    {
        public SuccessResult(string message) : base(true, message) { }
        public SuccessResult() : base(true) { }
    }
}
