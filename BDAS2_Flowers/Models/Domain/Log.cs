using System;

namespace BDAS2_Flowers.Models.Domain;
public class Log
{
    public int LogId { get; set; }
    public string OperationName { get; set; } = null!;
    public string TableName { get; set; } = null!;
    public DateTime ModificationDate { get; set; }
    public string ModificationBy { get; set; } = null!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}