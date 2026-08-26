using HotelResortMS.Core.Interfaces;

namespace HotelResortMS.Web.Models.Dashboard;

/// <summary>
/// Wraps IDashboardService's DashboardSnapshot with which sections the current user is
/// actually allowed to see (Section 19 - Role-Based Dashboard). Visibility is driven by
/// the same RolePermission data every other module already enforces, not a hard-coded
/// role-name check, so a custom role picks up the right dashboard sections automatically.
/// </summary>
public class DashboardViewModel
{
    public DashboardSnapshot Snapshot { get; set; } = new();

    public bool CanSeeFinancial { get; set; }
    public bool CanSeeRooms { get; set; }
    public bool CanSeeHousekeeping { get; set; }
    public bool CanSeeMaintenance { get; set; }
    public bool CanSeeReservations { get; set; }
    public bool CanSeeAmenities { get; set; }
    public bool CanSeePOS { get; set; }
    public bool CanSeeInventory { get; set; }
    public bool CanSeeFrontDesk { get; set; }
    public bool CanSeePayments { get; set; }
    public bool CanSeeGuests { get; set; }
    public bool CanSeeAuditTrail { get; set; }
}
