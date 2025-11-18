namespace BDAS2_Flowers.Models.ViewModels.AdminModels
{
    public class DbObjectRowVm
    {
        public string ObjectType { get; set; } = "";
        public string ObjectName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? Created { get; set; }
        public DateTime? LastDdlTime { get; set; }
    }

    public class DbObjectsVm
    {
        public IReadOnlyList<DbObjectRowVm> Objects { get; set; } = Array.Empty<DbObjectRowVm>();
    }
}
