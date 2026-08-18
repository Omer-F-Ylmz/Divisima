using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Return
{
    // Açıklayıcı yorum: Admin iade işleme (onay/ret).
    public class ReturnProcessRequestDto : IDto
    {
        public int return_id { get; set; }
        public bool approve { get; set; }
        public string? admin_note { get; set; }
    }
}
