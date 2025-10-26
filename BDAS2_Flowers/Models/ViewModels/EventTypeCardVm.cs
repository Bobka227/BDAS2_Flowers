namespace BDAS2_Flowers.Models.ViewModels
{
    public class EventTypeCardVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int OrdersCount { get; set; }
        public DateTime? LastEventDate { get; set; }
        public string ImageUrl { get; set; } = "/img/events/generic.jpg";
        public string? Subtitle { get; set; }
    }
}
