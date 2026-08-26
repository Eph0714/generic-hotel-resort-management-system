using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Infrastructure.Data;

/// <summary>
/// The system's single EF Core context. Extends IdentityDbContext so staff accounts, roles,
/// claims, etc. share the same database/migrations as the rest of the application - there is
/// no separate "auth database" to keep in sync.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<BusinessDate> BusinessDates => Set<BusinessDate>();
    public DbSet<DocumentNumberCounter> DocumentNumberCounters => Set<DocumentNumberCounter>();

    // Phase 2 - Hotel Operations
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<GuestIdentification> GuestIdentifications => Set<GuestIdentification>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<BedType> BedTypes => Set<BedType>();
    public DbSet<FloorArea> FloorAreas => Set<FloorArea>();
    public DbSet<RoomFeature> RoomFeatures => Set<RoomFeature>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<AmenityCategory> AmenityCategories => Set<AmenityCategory>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationRoom> ReservationRooms => Set<ReservationRoom>();
    public DbSet<ReservationAmenity> ReservationAmenities => Set<ReservationAmenity>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<CheckOut> CheckOuts => Set<CheckOut>();
    public DbSet<GuestFolio> GuestFolios => Set<GuestFolio>();
    public DbSet<FolioDetail> FolioDetails => Set<FolioDetail>();

    // Phase 3 - Sales
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<DiscountTransaction> DiscountTransactions => Set<DiscountTransaction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<POSTransaction> POSTransactions => Set<POSTransaction>();
    public DbSet<POSTransactionDetail> POSTransactionDetails => Set<POSTransactionDetail>();

    // Phase 4 - Inventory
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeDetail> RecipeDetails => Set<RecipeDetail>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();
    public DbSet<Receiving> Receivings => Set<Receiving>();
    public DbSet<ReceivingDetail> ReceivingDetails => Set<ReceivingDetail>();
    public DbSet<AccountsPayable> AccountsPayables => Set<AccountsPayable>();

    // Phase 5 - Finance
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AccountsReceivable> AccountsReceivables => Set<AccountsReceivable>();
    public DbSet<NightAuditRecord> NightAuditRecords => Set<NightAuditRecord>();
    public DbSet<DailyClosingRecord> DailyClosingRecords => Set<DailyClosingRecord>();

    // Phase 6 - Advanced Operations
    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();
    public DbSet<MaintenanceCategory> MaintenanceCategories => Set<MaintenanceCategory>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<EventVenue> EventVenues => Set<EventVenue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageComponent> PackageComponents => Set<PackageComponent>();
    public DbSet<CancellationPolicy> CancellationPolicies => Set<CancellationPolicy>();

    // Phase 8 - Backup/Restore
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RolePermission>(e =>
        {
            // One permission row per (Role, Module) - enforced so seeding/edits can never
            // create ambiguous duplicate grants for the same module.
            e.HasIndex(p => new { p.RoleId, p.Module }).IsUnique();
            e.HasOne(p => p.Role).WithMany().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SystemSetting>(e =>
        {
            e.HasIndex(s => s.Key).IsUnique();
        });

        builder.Entity<BusinessDate>(e =>
        {
            e.HasIndex(b => b.Date).IsUnique();
            e.Property(b => b.BeginningCash).HasPrecision(18, 2);
            e.Property(b => b.EndingCash).HasPrecision(18, 2);
        });

        builder.Entity<DocumentNumberCounter>(e =>
        {
            e.HasIndex(d => new { d.DocumentType, d.Year }).IsUnique();
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.Module);
            e.HasIndex(a => a.BusinessDate);
        });

        // --- Phase 2: Hotel Operations ---

        builder.Entity<Guest>(e =>
        {
            e.HasMany(g => g.Identifications).WithOne(i => i.Guest).HasForeignKey(i => i.GuestId);
        });

        builder.Entity<RoomType>(e =>
        {
            e.Property(r => r.RegularRate).HasPrecision(18, 2);
            e.Property(r => r.WeekendRate).HasPrecision(18, 2);
            e.Property(r => r.HolidayRate).HasPrecision(18, 2);
            e.Property(r => r.SeasonalRate).HasPrecision(18, 2);
            e.Property(r => r.ExtraPersonRate).HasPrecision(18, 2);
        });

        builder.Entity<Room>(e =>
        {
            // A room number identifies a physical room uniquely among currently-active
            // rooms; soft-deleted/archived numbers can be reused.
            e.HasIndex(r => r.RoomNumber);
            e.HasOne(r => r.RoomType).WithMany().HasForeignKey(r => r.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.BedType).WithMany().HasForeignKey(r => r.BedTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.FloorArea).WithMany().HasForeignKey(r => r.FloorAreaId).OnDelete(DeleteBehavior.SetNull);
            e.Property(r => r.RegularRateOverride).HasPrecision(18, 2);
            e.Property(r => r.WeekendRateOverride).HasPrecision(18, 2);
            e.Property(r => r.HolidayRateOverride).HasPrecision(18, 2);
            e.Property(r => r.SeasonalRateOverride).HasPrecision(18, 2);
            e.Property(r => r.ExtraPersonRateOverride).HasPrecision(18, 2);
        });

        builder.Entity<Amenity>(e =>
        {
            e.HasOne(a => a.AmenityCategory).WithMany().HasForeignKey(a => a.AmenityCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.HourlyRate).HasPrecision(18, 2);
            e.Property(a => a.DailyRate).HasPrecision(18, 2);
            e.Property(a => a.RegularRate).HasPrecision(18, 2);
            e.Property(a => a.WeekendRate).HasPrecision(18, 2);
            e.Property(a => a.HolidayRate).HasPrecision(18, 2);
            e.Property(a => a.SeasonalRate).HasPrecision(18, 2);
            e.Property(a => a.AdditionalChargePerHour).HasPrecision(18, 2);
        });

        builder.Entity<Reservation>(e =>
        {
            e.HasIndex(r => r.ReservationNumber).IsUnique();
            e.HasOne(r => r.Guest).WithMany().HasForeignKey(r => r.GuestId).OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.TotalAmount).HasPrecision(18, 2);
            e.Property(r => r.DiscountAmount).HasPrecision(18, 2);
            e.Property(r => r.DepositRequired).HasPrecision(18, 2);
            e.Property(r => r.AmountPaid).HasPrecision(18, 2);
            e.Property(r => r.BalanceDue).HasPrecision(18, 2);
            e.Property(r => r.CancellationFeeAmount).HasPrecision(18, 2);
            e.Property(r => r.PackagePrice).HasPrecision(18, 2);
            e.HasOne(r => r.CancellationPolicy).WithMany().HasForeignKey(r => r.CancellationPolicyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Package).WithMany().HasForeignKey(r => r.PackageId).OnDelete(DeleteBehavior.SetNull);

            e.HasMany(r => r.Rooms).WithOne(rr => rr.Reservation).HasForeignKey(rr => rr.ReservationId);
            e.HasMany(r => r.Amenities).WithOne(ra => ra.Reservation).HasForeignKey(ra => ra.ReservationId);
        });

        builder.Entity<ReservationRoom>(e =>
        {
            // Indexed (not unique) - overlap checking is a date-range query done in
            // ReservationService, not something a DB constraint alone can express.
            e.HasIndex(rr => rr.RoomId);
            e.HasOne(rr => rr.Room).WithMany().HasForeignKey(rr => rr.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.Property(rr => rr.RateAmount).HasPrecision(18, 2);
        });

        builder.Entity<ReservationAmenity>(e =>
        {
            e.HasOne(ra => ra.Amenity).WithMany().HasForeignKey(ra => ra.AmenityId).OnDelete(DeleteBehavior.Restrict);
            e.Property(ra => ra.RateAmount).HasPrecision(18, 2);
        });

        builder.Entity<CheckIn>(e =>
        {
            e.HasOne(c => c.Reservation).WithMany().HasForeignKey(c => c.ReservationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CheckOut>(e =>
        {
            e.HasOne(c => c.Reservation).WithMany().HasForeignKey(c => c.ReservationId).OnDelete(DeleteBehavior.Restrict);
            e.Property(c => c.FinalBalance).HasPrecision(18, 2);
        });

        builder.Entity<GuestFolio>(e =>
        {
            e.HasIndex(f => f.FolioNumber).IsUnique();
            e.HasOne(f => f.Reservation).WithMany().HasForeignKey(f => f.ReservationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Guest).WithMany().HasForeignKey(f => f.GuestId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(f => f.Details).WithOne(d => d.GuestFolio).HasForeignKey(d => d.GuestFolioId);
        });

        builder.Entity<FolioDetail>(e =>
        {
            e.Property(d => d.Amount).HasPrecision(18, 2);
        });

        // --- Phase 3: Sales ---

        builder.Entity<Product>(e =>
        {
            e.HasOne(p => p.ProductCategory).WithMany().HasForeignKey(p => p.ProductCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.UnitPrice).HasPrecision(18, 2);
            e.Property(p => p.Cost).HasPrecision(18, 2);
        });

        builder.Entity<Discount>(e =>
        {
            e.Property(d => d.Percentage).HasPrecision(18, 4);
            e.Property(d => d.FixedAmount).HasPrecision(18, 2);
        });

        builder.Entity<DiscountTransaction>(e =>
        {
            e.HasOne(t => t.Discount).WithMany().HasForeignKey(t => t.DiscountId).OnDelete(DeleteBehavior.Restrict);
            e.Property(t => t.EligibleAmount).HasPrecision(18, 2);
            e.Property(t => t.DiscountAmount).HasPrecision(18, 2);
            e.HasIndex(t => new { t.ReferenceType, t.ReferenceId });
        });

        builder.Entity<Payment>(e =>
        {
            e.HasIndex(p => p.PaymentNumber).IsUnique();
            e.HasOne(p => p.Guest).WithMany().HasForeignKey(p => p.GuestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.GuestFolio).WithMany().HasForeignKey(p => p.GuestFolioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.PosTransaction).WithMany().HasForeignKey(p => p.PosTransactionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.Amount).HasPrecision(18, 2);
        });

        builder.Entity<POSTransaction>(e =>
        {
            e.HasIndex(p => p.PosNumber).IsUnique();
            e.HasOne(p => p.Guest).WithMany().HasForeignKey(p => p.GuestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.GuestFolio).WithMany().HasForeignKey(p => p.GuestFolioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Income).WithMany().HasForeignKey(p => p.IncomeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.GrossAmount).HasPrecision(18, 2);
            e.Property(p => p.DiscountAmount).HasPrecision(18, 2);
            e.Property(p => p.TaxableAmount).HasPrecision(18, 2);
            e.Property(p => p.TaxAmount).HasPrecision(18, 2);
            e.Property(p => p.ServiceChargeAmount).HasPrecision(18, 2);
            e.Property(p => p.NetAmount).HasPrecision(18, 2);
            e.HasMany(p => p.Details).WithOne(d => d.POSTransaction).HasForeignKey(d => d.POSTransactionId);
        });

        builder.Entity<POSTransactionDetail>(e =>
        {
            e.HasOne(d => d.Product).WithMany().HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.UnitPrice).HasPrecision(18, 2);
            e.Property(d => d.LineTotal).HasPrecision(18, 2);
        });

        // --- Phase 4: Inventory ---

        builder.Entity<InventoryItem>(e =>
        {
            e.HasOne(i => i.UnitOfMeasure).WithMany().HasForeignKey(i => i.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.InventoryLocation).WithMany().HasForeignKey(i => i.InventoryLocationId).OnDelete(DeleteBehavior.Restrict);
            e.Property(i => i.Cost).HasPrecision(18, 4);
            e.Property(i => i.CurrentStock).HasPrecision(18, 4);
            e.Property(i => i.ReorderLevel).HasPrecision(18, 4);
        });

        builder.Entity<Product>(e =>
        {
            e.HasOne(p => p.InventoryItem).WithMany().HasForeignKey(p => p.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InventoryTransaction>(e =>
        {
            e.HasOne(t => t.InventoryItem).WithMany().HasForeignKey(t => t.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(t => t.Quantity).HasPrecision(18, 4);
            e.HasIndex(t => t.InventoryItemId);
            e.HasIndex(t => new { t.ReferenceType, t.ReferenceId });
        });

        builder.Entity<Recipe>(e =>
        {
            e.HasIndex(r => r.ProductId).IsUnique();
            e.HasOne(r => r.Product).WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Components).WithOne(c => c.Recipe).HasForeignKey(c => c.RecipeId);
        });

        builder.Entity<RecipeDetail>(e =>
        {
            e.HasOne(c => c.InventoryItem).WithMany().HasForeignKey(c => c.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(c => c.QuantityRequired).HasPrecision(18, 4);
        });

        builder.Entity<PurchaseOrder>(e =>
        {
            e.HasIndex(p => p.PONumber).IsUnique();
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.TotalAmount).HasPrecision(18, 2);
            e.HasMany(p => p.Details).WithOne(d => d.PurchaseOrder).HasForeignKey(d => d.PurchaseOrderId);
        });

        builder.Entity<PurchaseOrderDetail>(e =>
        {
            e.HasOne(d => d.InventoryItem).WithMany().HasForeignKey(d => d.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.QuantityOrdered).HasPrecision(18, 4);
            e.Property(d => d.QuantityReceived).HasPrecision(18, 4);
            e.Property(d => d.UnitCost).HasPrecision(18, 4);
        });

        builder.Entity<Receiving>(e =>
        {
            e.HasIndex(r => r.ReceivingNumber).IsUnique();
            e.HasOne(r => r.PurchaseOrder).WithMany().HasForeignKey(r => r.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Details).WithOne(d => d.Receiving).HasForeignKey(d => d.ReceivingId);
        });

        builder.Entity<ReceivingDetail>(e =>
        {
            e.HasOne(d => d.PurchaseOrderDetail).WithMany().HasForeignKey(d => d.PurchaseOrderDetailId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.InventoryItem).WithMany().HasForeignKey(d => d.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.QuantityReceived).HasPrecision(18, 4);
            e.Property(d => d.UnitCost).HasPrecision(18, 4);
            e.Property(d => d.QuantityDamaged).HasPrecision(18, 4);
        });

        builder.Entity<AccountsPayable>(e =>
        {
            e.HasOne(a => a.Supplier).WithMany().HasForeignKey(a => a.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.PurchaseOrder).WithMany().HasForeignKey(a => a.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Amount).HasPrecision(18, 2);
            e.Property(a => a.AmountPaid).HasPrecision(18, 2);
            e.Property(a => a.Balance).HasPrecision(18, 2);
        });

        // --- Phase 5: Finance ---

        builder.Entity<Income>(e =>
        {
            e.HasIndex(i => i.IncomeNumber).IsUnique();
            e.HasIndex(i => new { i.ReferenceType, i.ReferenceId });
            e.Property(i => i.GrossAmount).HasPrecision(18, 2);
            e.Property(i => i.DiscountAmount).HasPrecision(18, 2);
            e.Property(i => i.NetAmount).HasPrecision(18, 2);
        });

        builder.Entity<Expense>(e =>
        {
            e.HasIndex(x => x.ExpenseNumber).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        builder.Entity<AccountsReceivable>(e =>
        {
            e.HasOne(a => a.Guest).WithMany().HasForeignKey(a => a.GuestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Reservation).WithMany().HasForeignKey(a => a.ReservationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.GuestFolio).WithMany().HasForeignKey(a => a.GuestFolioId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Amount).HasPrecision(18, 2);
            e.Property(a => a.AmountPaid).HasPrecision(18, 2);
            e.Property(a => a.Balance).HasPrecision(18, 2);
        });

        builder.Entity<NightAuditRecord>(e =>
        {
            e.HasOne(n => n.BusinessDate).WithMany().HasForeignKey(n => n.BusinessDateId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DailyClosingRecord>(e =>
        {
            e.HasOne(d => d.BusinessDate).WithMany().HasForeignKey(d => d.BusinessDateId).OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.BeginningCash).HasPrecision(18, 2);
            e.Property(d => d.GrossRevenue).HasPrecision(18, 2);
            e.Property(d => d.DiscountAmount).HasPrecision(18, 2);
            e.Property(d => d.NetRevenue).HasPrecision(18, 2);
            e.Property(d => d.CashReceipts).HasPrecision(18, 2);
            e.Property(d => d.CreditSales).HasPrecision(18, 2);
            e.Property(d => d.ExpensesCash).HasPrecision(18, 2);
            e.Property(d => d.ExpensesOther).HasPrecision(18, 2);
            e.Property(d => d.ExpectedEndingCash).HasPrecision(18, 2);
            e.Property(d => d.ActualCashCount).HasPrecision(18, 2);
            e.Property(d => d.CashVariance).HasPrecision(18, 2);
        });

        // --- Phase 6: Advanced Operations ---

        builder.Entity<HousekeepingTask>(e =>
        {
            e.HasOne(t => t.Room).WithMany().HasForeignKey(t => t.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AssignedToUser).WithMany().HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.InspectedByUser).WithMany().HasForeignKey(t => t.InspectedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Equipment>(e =>
        {
            e.HasOne(eq => eq.Room).WithMany().HasForeignKey(eq => eq.RoomId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MaintenanceRequest>(e =>
        {
            e.HasIndex(m => m.RequestNumber).IsUnique();
            e.HasOne(m => m.MaintenanceCategory).WithMany().HasForeignKey(m => m.MaintenanceCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Room).WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(m => m.Equipment).WithMany().HasForeignKey(m => m.EquipmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(m => m.AssignedToUser).WithMany().HasForeignKey(m => m.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            e.Property(m => m.Cost).HasPrecision(18, 2);
        });

        builder.Entity<Event>(e =>
        {
            e.HasIndex(ev => ev.EventNumber).IsUnique();
            e.HasOne(ev => ev.EventType).WithMany().HasForeignKey(ev => ev.EventTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.EventVenue).WithMany().HasForeignKey(ev => ev.EventVenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.Guest).WithMany().HasForeignKey(ev => ev.GuestId).OnDelete(DeleteBehavior.SetNull);
            e.Property(ev => ev.TotalAmount).HasPrecision(18, 2);
            e.Property(ev => ev.DepositAmount).HasPrecision(18, 2);
            e.Property(ev => ev.AmountPaid).HasPrecision(18, 2);
            e.Property(ev => ev.BalanceDue).HasPrecision(18, 2);
        });

        builder.Entity<Package>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.HasMany(p => p.Components).WithOne(c => c.Package).HasForeignKey(c => c.PackageId);
        });

        builder.Entity<CancellationPolicy>(e =>
        {
            e.Property(p => p.FeePercentage).HasPrecision(18, 4);
            e.Property(p => p.FixedFee).HasPrecision(18, 2);
        });
    }
}
