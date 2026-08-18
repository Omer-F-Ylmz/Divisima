namespace Divisima.Core.Utilities.Results
{
    // Açıklayıcı yorum: Hatalı sonuç (veri yok).
    public class ErrorResult : Result
    {
        public ErrorResult(string message) : base(false, message) { }
        public ErrorResult() : base(false) { }
    }
}
