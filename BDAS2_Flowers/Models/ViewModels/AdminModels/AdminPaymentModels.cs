using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.AdminModels;

public class AdminPaymentRowVm
{
    public int Id { get; set; }
    public DateTime PayDate { get; set; }
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }

    public decimal? Accepted { get; set; }
    public decimal? Returned { get; set; }
    public string? CardNumber { get; set; }
    public DateTime? CouponDate { get; set; }
    public decimal? Bonus { get; set; }
}


public class AdminPaymentEditVm
{
    public int? Id { get; set; }

    [Required]
    public DateTime PayDate { get; set; } = DateTime.Today;

    [Required]
    [Range(0.01, 999999)]
    public decimal Amount { get; set; }

    [Required]
    public string MethodCode { get; set; } = "cash"; 
}

