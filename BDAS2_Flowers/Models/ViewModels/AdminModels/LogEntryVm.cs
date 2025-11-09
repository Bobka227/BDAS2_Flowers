namespace BDAS2_Flowers.Models.ViewModels.AdminModels;

public class LogEntryVm
{
    public long LogId { get; set; }
    public string OperationName { get; set; } = "";
    public string TableName { get; set; } = "";
    public DateTime ModificationDate { get; set; }
    public string ModificationBy { get; set; } = "";
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}

public class LogsPageVm
{
    public IReadOnlyList<LogEntryVm> Items { get; set; } = Array.Empty<LogEntryVm>();
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public long Total { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
