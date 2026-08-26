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
}
