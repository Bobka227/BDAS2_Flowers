using System;

namespace BDAS2_Flowers.Models.Domain;
public class Event
{
    public int EventId { get; set; }
    public DateTime EventDate { get; set; }
    public int Order_OrderId { get; set; }
    public int Event_Type_EventTypeId { get; set; }
}