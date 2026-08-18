using Divisima.Core.Utilities.Results;

namespace Divisima.Core.Utilities.Business
{
    // Açıklayıcı yorum: İş kuralı zinciri (Cafixo BusinessRules kalıbı). İlk hatada durur.
    public static class BusinessRules
    {
        public static IResult Run(params IResult[] logics)
        {
            foreach (var logic in logics)
            {
                if (!logic.Success) return logic;
            }
            return null;
        }
    }
}
