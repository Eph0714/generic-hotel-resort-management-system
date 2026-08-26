namespace HotelResortMS.Core.Common;

/// <summary>The set of controllable actions from Section 55 (CRUD Permission Control).</summary>
public enum PermissionAction
{
    View,
    Add,
    Edit,
    Delete,
    Approve,
    Void,
    Refund,
    Print,
    Export,
    Configure
}

/// <summary>
/// Canonical list of module keys used by RolePermission rows and by [RequirePermission] on controllers.
/// New modules get added here as later phases introduce them - this is the single source of truth so
/// the permission matrix screen and the enforcement filter never drift apart.
/// </summary>
public static class SystemModules
{
    // Administration (Phase 1)
    public const string Users = "Users";
    public const string Roles = "Roles";
    public const string SystemSettings = "SystemSettings";
    public const string AuditTrail = "AuditTrail";
    public const string BackupRestore = "BackupRestore";

    // Placeholders wired up in later phases (kept here so the permission matrix can pre-render them).
    public const string Dashboard = "Dashboard";
    public const string Guests = "Guests";
    public const string Rooms = "Rooms";
    public const string Amenities = "Amenities";
    public const string Reservations = "Reservations";
    public const string FrontDesk = "FrontDesk";
    public const string GuestFolio = "GuestFolio";
    public const string POS = "POS";
    public const string Products = "Products";
    public const string Payments = "Payments";
    public const string Discounts = "Discounts";
    public const string Inventory = "Inventory";
    public const string Suppliers = "Suppliers";
    public const string Purchasing = "Purchasing";
    public const string Income = "Income";
    public const string Expenses = "Expenses";
    public const string AccountsReceivable = "AccountsReceivable";
    public const string AccountsPayable = "AccountsPayable";
    public const string BusinessDate = "BusinessDate";
    public const string NightAudit = "NightAudit";
    public const string DailyClosing = "DailyClosing";
    public const string Housekeeping = "Housekeeping";
    public const string Maintenance = "Maintenance";
    public const string Events = "Events";
    public const string Packages = "Packages";
    public const string Reports = "Reports";

    public static readonly string[] All =
    {
        Dashboard, Guests, Rooms, Amenities, Reservations, FrontDesk, GuestFolio,
        POS, Products, Payments, Discounts,
        Inventory, Suppliers, Purchasing,
        Income, Expenses, AccountsReceivable, AccountsPayable, BusinessDate, NightAudit, DailyClosing,
        Housekeeping, Maintenance, Events, Packages,
        Reports,
        Users, Roles, SystemSettings, AuditTrail, BackupRestore
    };
}

/// <summary>Built-in roles seeded at startup (Section 45). Super Admin always has every permission.</summary>
public static class SystemRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string Administrator = "Administrator";
    public const string FrontDesk = "Front Desk";
    public const string POSStaff = "POS Staff";
    public const string InventoryStaff = "Inventory Staff";
    public const string AccountantCashier = "Accountant/Cashier";

    public static readonly string[] All =
    {
        SuperAdmin, Administrator, FrontDesk, POSStaff, InventoryStaff, AccountantCashier
    };
}
