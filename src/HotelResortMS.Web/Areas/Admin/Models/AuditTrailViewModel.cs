using HotelResortMS.Core.Entities;

namespace HotelResortMS.Web.Areas.Admin.Models;

public class AuditTrailViewModel
{
    public List<AuditLog> Logs { get; set; } = new();
    public string? ModuleFilter { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public List<string> Modules { get; set; } = new();

    /// <summary>Current System Settings > Audit.RetentionDays value, shown so a Super
    /// Admin can see at a glance how long entries stick around before the automatic
    /// cleanup removes them.</summary>
    public int RetentionDays { get; set; } = 3;
}
