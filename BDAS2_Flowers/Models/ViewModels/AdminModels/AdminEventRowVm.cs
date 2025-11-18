namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class AdminEventRowVm
    {
        public int EventId { get; set; }
        public string OrderNo { get; set; } = "";    
        public DateTime EventDate { get; set; }    
        public string EventType { get; set; } = "";  
        public string Customer { get; set; } = ""; 
        public string Status { get; set; } = "";    
        public string Delivery { get; set; } = "";  
        public string Shop { get; set; } = "";      
        public decimal Total { get; set; }         
    }
}
