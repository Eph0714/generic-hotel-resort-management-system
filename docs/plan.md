# Generic Hotel and Resort Management System — Build Plan

## Context

The user wants a complete, production-grade Hotel and Resort Management System built
in `C:\Users\Ephraim\Desktop\GENERIC HOTEL AND RESORT MANAGEMENT SYSTEM`, per a 61-section
master spec (ASP.NET Core MVC + EF Core + MySQL, covering reservations, front desk, POS,
inventory, purchasing, AR/AP, business date/night audit/daily closing, housekeeping,
maintenance, events, packages, reports, and admin/security). The spec explicitly says
(§59) not to build this as one giant untested blob — build it in 8 controlled phases,
verifying and fixing at each boundary before moving on. This mirrors how the user's
TWINS SYSTEM project was built ([[project_twins_system_build]],
[[feedback_verify_then_auto_advance_phases]]), and the user has confirmed the same
approach here: **auto-advance through all 8 phases**, running a full verify/fix sweep at
each boundary and continuing without stopping to ask, reporting progress as I go.

Database: **MySQL** (user's explicit choice over SQL Server LocalDB). The machine already
runs `MySQL56` (port 3306, dedicated to TAM-AN FMS — never touch) and a manually-managed
MySQL 8.4 instance for TWINS SYSTEM (port 3307, started via `TWINS SYSTEM/scripts/mysql-*.ps1`,
not a Windows service). This project will follow the same proven pattern: its own
manually-managed MySQL 8.4 data directory and instance, on **port 3309** (3308 is already
in use by an unidentified process), using EF Core's `Pomelo.EntityFrameworkCore.MySql`
provider. Scripts will be adapted from `TWINS SYSTEM/scripts/mysql-*.ps1` as a template.

## Already scaffolded (before plan mode interrupted, still on disk, will be adjusted)

- Solution `HotelResortMS.sln` with 3 projects under `src/`:
  - `HotelResortMS.Core` (domain: entities, enums, no EF/infra dependencies except
    `Microsoft.Extensions.Identity.Stores` for `IdentityUser`/`IdentityRole` base types)
  - `HotelResortMS.Infrastructure` (EF Core DbContext, migrations, service implementations)
  - `HotelResortMS.Web` (ASP.NET Core MVC, scaffolded with `--auth Individual`, Identity UI)
- Core entities written so far: `Entities/BaseEntity.cs`, `Entities/Identity/ApplicationUser.cs`,
  `Entities/Identity/ApplicationRole.cs`, `Entities/Identity/RolePermission.cs`,
  `Common/PermissionAction.cs` (action enum + `SystemModules` + `SystemRoles` constants),
  `Entities/AuditLog.cs`.
- **Needs adjustment during execution**: Web/Infrastructure csproj currently reference
  `Microsoft.EntityFrameworkCore.SqlServer` — swap to `Pomelo.EntityFrameworkCore.MySql`
  (latest stable for EF Core 8, currently 8.0.x) + `Microsoft.EntityFrameworkCore.Design`/`Tools`.
  The scaffolded `Program.cs` still wires SQLite — replace with MySQL via Pomelo,
  `UseMySql(connectionString, ServerVersion.AutoDetect(...))`.

## Phase-by-phase plan (per spec §59, auto-advancing)

### Phase 1 — Foundation (build now)
- MySQL instance: adapt `TWINS SYSTEM/scripts/mysql-*.ps1` → new `scripts/mysql-*.ps1` in
  this project, data dir under the project (or a sibling data folder), port 3309,
  its own root/app credentials, `mysql-install-service.ps1` optional (match TWINS' pattern
  of a manually started instance unless a service is cleanly separable).
- Finish Core entities: `SystemSetting`, `BusinessDate` (§12: current/previous business
  date, open/close time+status), `Permission` wiring already via `RolePermission`.
- `HotelResortMS.Infrastructure`:
  - `ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>`
    with `DbSet<RolePermission>`, `DbSet<AuditLog>`, `DbSet<SystemSetting>`, `DbSet<BusinessDate>`.
  - `Services/AuditService.cs` (implements `IAuditService` from Core) — central write path
    for every audit row (§46, §56).
  - `Services/BusinessDateService.cs` (implements `IBusinessDateService`) — exposes current
    business date, daily opening (with duplicate-open prevention per §41), used by every
    later financial module instead of `DateTime.Now`.
  - `Services/PermissionService.cs` (implements `IPermissionService`) — `HasPermissionAsync(userId, module, action)`,
    Super Admin short-circuits to true for everything.
  - `Services/CurrentUserService.cs` — wraps `IHttpContextAccessor` for the current user id/name/IP.
  - `Data/DbSeeder.cs` — seeds `SystemRoles.All`, a default Super Admin user (generated
    strong password reported to the user, per [[feedback_show_local_url_after_build]]),
    full `RolePermission` grants for Super Admin across `SystemModules.All`, and a starter
    `SystemSettings` row (hotel name, currency, tax %, etc. — all placeholders, per §49 "do
    not hard-code hotel-specific settings").
  - `DependencyInjection.cs` extension (`AddInfrastructure(config)`) to keep `Program.cs` thin.
- `HotelResortMS.Web`:
  - Custom `RequirePermissionAttribute` (action filter) checking `IPermissionService` —
    enforced server-side per §55, not just hidden buttons.
  - Admin area: Users CRUD, Roles CRUD (system roles protected from delete per
    `IsSystemRole`), Role-Permission matrix screen (checkbox grid: module × action),
    System Settings CRUD, Audit Trail viewer (read-only, filterable/paginated).
  - Shared layout: sidebar nav grouped by the spec's module groups (Operations, Sales,
    Inventory, Finance, Reports, Administration), Bootstrap, top nav with user/logout.
  - Dashboard shell (controller + view) with placeholder cards wired to real counts only
    where Phase 1 data exists (Users, Roles); later phases fill in the rest (§11).
  - `EF Core` migrations applied against the new MySQL instance; `dotnet run` verified to
    boot, log in as seeded Super Admin, and reach the dashboard.
- **Verify-and-fix sweep before advancing**: `dotnet build` (0 warnings treated as errors
  where feasible), `dotnet ef database update` succeeds clean, `dotnet run` boots and the
  seeded login works end-to-end in a browser check.

### Phase 2 — Hotel Operations
Guests (+ types/addresses/IDs/notes), Rooms (+ types/bed types/features/floors), Amenities
(+ categories), centralized Rate Management (§15, never overwrite historical rates),
Reservations (double-booking prevention via `ReservationService`, §21), Check-In/Check-Out
workflow (§24/§28), Guest Folio (§25).

### Phase 3 — Sales
Products/Product Categories, POS (§26, POS→Inventory→Folio→Payment→Income chain),
Payments (§27), centralized `DiscountService` (§18, Senior Citizen/PWD/promotional/etc.,
manual-override authorization + audit trail).

### Phase 4 — Inventory
Inventory/Locations/Units, Suppliers, Purchase Orders → Receiving → stock update →
payable (§35), Recipes/BOM (§34, POS sale → recipe → ingredient deduction), stock
transactions (no unrestricted delete once stock is affected, §33).

### Phase 5 — Finance
Income, Expenses, AR, AP, Business Date Daily Opening (§41), Night Audit (§42, blocks
close while unresolved exceptions exist unless authorized override), Daily Closing (§43,
cash formula, locks the business date, carries ending cash forward).

### Phase 6 — Advanced Operations
Housekeeping, Maintenance (auto room-status changes), Events/Function Halls, Packages,
configurable Cancellation Policies.

### Phase 7 — Reports
Operational/Reservation/Room/Amenity/POS/Inventory/Financial/Discount/Audit reports, all
with search/filter/date-range/print/PDF/Excel export.

### Phase 8 — Testing and Optimization
Work through the §58 test matrix (double-booking, POS void/refund, SC/PWD discount edge
cases, accounting balance flows, inventory deduction/adjustment, security/CSRF/permission
checks, backup/restore), fix everything found, check UI responsiveness and performance.

## Key architectural rules carried through every phase
- No business logic in controllers/views — always through the Service layer listed in
  the spec (§1): `ReservationService`, `RoomService`, `FolioService`, `PaymentService`,
  `DiscountService`, `POSService`, `InventoryService`, `AccountingService`,
  `BusinessDateService`, `NightAuditService`, etc.
- Every entity needing history/financial linkage uses soft-delete/archive fields from
  `BaseEntity`, never hard delete (§9, §10) — delete attempts must check references first
  and show the "cannot delete, has history — deactivate/archive instead" message (§8).
- All money fields `decimal` with explicit precision; all financial/reservation writes
  wrapped in DB transactions (§53).
- Every document type gets its own auto-numbering (`RES-2026-000001` etc.) via a shared
  numbering service, configurable prefix (§47).
- `RequirePermission` + `IAuditService` used consistently — no module ships without both.

## Verification (per phase, before auto-advancing)
1. `dotnet build HotelResortMS.sln` — must be clean.
2. `dotnet ef database update` (Infrastructure→Web) against the dedicated MySQL 8.4
   instance — must apply without error.
3. `dotnet run --project src/HotelResortMS.Web` — confirm the app boots, log in as the
   seeded Super Admin account, and click through that phase's new screens.
4. Report the local URL + Super Admin credentials after every successful run
   ([[feedback_show_local_url_after_build]]).
5. Fix anything broken before starting the next phase; never carry a known defect forward.
