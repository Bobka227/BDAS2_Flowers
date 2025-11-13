namespace BDAS2_Flowers.Models.ViewModels.ReviewModels
{
    public class ReviewRowVm
    {
        public string Name { get; set; } = ""; 
        public string City { get; set; } = "";
        public int Stars { get; set; }
        public DateTime Created { get; set; }
        public string Text { get; set; } = "";
    }

    public class ReviewsPageVm
    {
        public List<ReviewRowVm> Reviews { get; set; } = new();

        public double Average => Reviews.Count == 0 ? 0.0 : Reviews.Average(r => r.Stars);
        public int Count => Reviews.Count;

        public int? NewStars { get; set; }
        public string? NewText { get; set; }
        public string? NewCity { get; set; }
    }
}
