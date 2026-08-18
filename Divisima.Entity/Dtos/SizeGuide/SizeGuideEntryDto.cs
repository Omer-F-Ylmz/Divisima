using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.SizeGuide
{
    public class SizeGuideEntryDto : IDto
    {
        public int category_id { get; set; }
        public string size_label { get; set; }
        public decimal? bust_cm { get; set; }
        public decimal? waist_cm { get; set; }
        public decimal? hip_cm { get; set; }
        public decimal? length_cm { get; set; }
        public int sort_order { get; set; }
    }
}
