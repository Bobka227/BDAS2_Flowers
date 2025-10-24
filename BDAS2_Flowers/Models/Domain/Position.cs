namespace BDAS2_Flowers.Models.Domain;
public class Position
{
    public int PositionId { get; set; }
    public string PositionName { get; set; } = null!;
    public int EmployeerId { get; set; }
}
