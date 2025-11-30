using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.AdminModels;

public class AdminEmployeeRowVm
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime EmploymentDate { get; set; }
    public decimal Salary { get; set; }
    public string Shop { get; set; } = "";
    public string Position { get; set; } = "";
    public string Manager { get; set; } = "";

    public decimal TeamSalary { get; set; }
}


public class AdminEmployeeEditVm
{
    public int? Id { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Jméno")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(50)]
    [Display(Name = "Příjmení")]
    public string LastName { get; set; } = "";

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Datum nástupu")]
    public DateTime EmploymentDate { get; set; } = DateTime.Today;

    [Range(0, 100000)]
    [Display(Name = "Plat")]
    public decimal Salary { get; set; }

    [Required]
    [Display(Name = "Prodejna")]
    public int ShopId { get; set; }

    [Display(Name = "Pozice")]
    public int? PositionId { get; set; }

    [Display(Name = "Nadřízený")]
    public int? ManagerId { get; set; }
}

public class AdminEmployeeTreeRowVm
{
    public int Id { get; set; }
    public string IndentedName { get; set; } = "";
    public int Level { get; set; }

    public string Manager { get; set; } = "";
    public string RootName { get; set; } = "";
    public string OrgPath { get; set; } = "";
    public string Shop { get; set; } = "";
    public string Position { get; set; } = "";
}