using System.ComponentModel.DataAnnotations;

namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class AdminPositionRowVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int EmployeesCount { get; set; }
    }

    public class AdminPositionEditVm
    {
        public int? Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        public int EmployeeCount { get; set; }
        public int EmployeesUsing { get; set; }
    }
}
