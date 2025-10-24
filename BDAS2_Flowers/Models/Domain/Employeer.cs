using System;

namespace BDAS2_Flowers.Models.Domain;
public class Employeer
{
    public int EmployeerId { get; set; }
    public DateTime EmploymentDate { get; set; }
    public int Salary { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int? ShopId { get; set; }
    public int? EmployeerId_1 { get; set; }
}
