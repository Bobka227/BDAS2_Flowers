namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class AdminReviewRowVm
    {
        public int ReviewId { get; set; }
        public string UserDisplay { get; set; } = "";
        public string City { get; set; } = "";
        public int Stars { get; set; }
        public DateTime Created { get; set; }
        public string Text { get; set; } = "";
    }
}
