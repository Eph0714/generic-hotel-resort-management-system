# Generic Hotel and Resort Management System — Build Plan

Full spec: `HOTEL AND RESORT MANAGEMENT SYSTEM.docx` (61 sections). Built phase by phase per
Section 59; each phase ends with a build+migrate+run verification sweep before the next
phase starts. Detailed architectural plan: see `docs/plan.md` (copy of the approved plan).

Stack: ASP.NET Core MVC 8, EF Core 8 (Pomelo MySQL provider), MySQL 8.4 on port 3309
(dedicated instance, see `scripts/mysql-*.ps1`), 3-project clean architecture
(`HotelResortMS.Core` / `HotelResortMS.Infrastructure` / `HotelResortMS.Web`).

## Status — all 8 phases complete

- [x] **Phase 1 — Foundation**: solution scaffold, MySQL instance, ApplicationDbContext
      (Identity + RolePermission + AuditLog + SystemSetting + BusinessDate + DocumentNumberCounter),
      AuditService, BusinessDateService, PermissionService (+ RequirePermission filter),
      NumberingService, DbSeeder (6 system roles, Super Admin, starter settings), sidebar
      layout, Users/Roles+Permission-matrix/SystemSettings/AuditTrail admin screens,
      Dashboard shell. Verified: clean build, migration applies, login as seeded Super
      Admin, all Phase 1 screens reachable, audit trail records login.
- [x] **Phase 2 — Hotel Operations**: Guests CRUD (+ Senior/PWD fields, reservation history),
      Room Types/Bed Types/Floors/Amenity Categories master data, Rooms + Amenities CRUD
      with live Status Boards, ReservationService (double-booking prevention via date-range
      overlap check, re-validated inside a DB transaction), Reservations CRUD with an
      availability-aware room picker, FrontDeskService (Check-In opens a Guest Folio seeded
      with room charges/discount/deposit; Check-Out blocks on outstanding balance unless
      authorized, then hands the room to Housekeeping as Dirty). Verified end-to-end:
      reservation → check-in → folio → authorized checkout → room status Dirty, audit
      trail records every step. Fixed a bug: NumberingService opened its own DB
      transaction even when called from inside an ambient one, which EF Core rejects -
      now joins the ambient transaction instead.
- [x] **Phase 3 — Sales**: Products/Categories, centralized DiscountService (gross→discount→
      tax→net, Senior Citizen/PWD/Promotional types), POS sales with room-charge or direct
      payment, Payments (Void/Refund), sale Void/Refund reversing the linked payment.
      Verified end-to-end with correct tax/discount math on a live sale.
- [x] **Phase 4 — Inventory**: Units/Locations/Items, Suppliers, Purchase Orders → Submit →
      Receive → stock update → Accounts Payable, Recipes/BOM, POS sale correctly deducting
      stock (direct-link or recipe) with Void/Refund reversing the deduction. Verified the
      full chain live against the running app.
- [x] **Phase 5 — Finance**: Income (auto-posted by POS/Check-In at the point revenue is
      recognized, not when collected), Expenses, Accounts Receivable (auto-created on an
      authorized outstanding checkout balance), Business Date/Night Audit/Daily Closing.
      Full cycle tested: open → sell → expense → audit → close → reopen → open-next-day,
      cash math and carried-forward beginning cash all correct. **Fixed a real bug**:
      `BusinessDateService.GetCurrentAsync` was silently fabricating a new "today" business
      date whenever the latest one was Closed, discarding the carried ending cash and
      bypassing the explicit Open-Next-Day step - fixed to only bootstrap on a genuinely
      empty table, plus added `GetCurrentForPostingAsync()` to block new financial
      postings against a Closed date.
- [x] **Phase 6 — Advanced Operations**: Housekeeping (Dirty→Cleaning→Clean→Inspected→Ready,
      auto-created at Check-Out and after Maintenance completes), Maintenance (auto
      room-status change), Events (venue double-booking check + revenue recognition),
      Packages, Cancellation Policies (fee computed at cancellation, capped at amount
      actually paid). **Fixed a real bug**: `HousekeepingTask.InspectedByUserId` (a real FK
      to AspNetUsers) was being set from the inspector's display name/email instead of
      their actual user Id, causing a 500 on every inspection.
- [x] **Phase 7 — Reports**: Hotel/Reservations/Amenities/POS/Inventory/Financial/Discounts
      report screens, each with date-range filtering, CSV export (Excel-compatible,
      verified correct BOM/headers), and browser print/PDF - a deliberate scoping choice
      over a bespoke PDF-generation library. Cross-checked Financial Profit/Loss and
      Inventory valuation against raw DB state - both matched exactly.
- [x] **Phase 8 — Testing and Optimization**: built Backup/Restore (§50 - was genuinely
      missing, not just untested), then ran the §58 test matrix live against the app.
      **Fixed two more real bugs**: (1) Senior Citizen/PWD discounts had no eligibility
      enforcement at all - added a check in `DiscountService` against the guest's
      `IsSeniorCitizen`/`IsPwd` flag + ID-on-file (bypassable only via an authorized manual
      override), plus a duplicate-discount-per-transaction guard; (2)
      `RequirePermissionAttribute`'s Login/AccessDenied redirects didn't clear the ambient
      `area` route value, so a permission failure inside any Area-scoped controller (e.g.
      `Admin/Users`) 404'd instead of showing the 403 page. Also verified clean:
      double-booking prevention, negative-inventory prevention, and CSRF protection (400
      on a token-less POST even for the fully-permissioned Super Admin).

## Running locally

```powershell
.\scripts\mysql-start.ps1        # starts the dedicated MySQL 8.4 instance on port 3309
dotnet run --project src/HotelResortMS.Web
```

First run seeds the Super Admin account and prints its email + a generated password to the
console once — save it, it is not shown again. Reset by deleting the `hotel_resort_ms`
database and re-running.

## Configuration and secrets

`appsettings.json` contains only placeholder values (`CHANGE_ME`) — it is safe to commit.
Real values are supplied per environment:

- **Local development**: via .NET User Secrets (already configured on this machine —
  `ConnectionStrings:DefaultConnection`, `Backup:MySqlBinDirectory`, `Backup:Directory`).
  View/edit with `dotnet user-secrets list --project src/HotelResortMS.Web` /
  `dotnet user-secrets set "<key>" "<value>" --project src/HotelResortMS.Web`. Secrets live
  outside the repo (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`) and are
  loaded automatically only when `ASPNETCORE_ENVIRONMENT=Development`.
- **Any other environment** (staging/production): set the same keys as environment
  variables using ASP.NET Core's `:` → `__` convention, e.g.
  `ConnectionStrings__DefaultConnection`, `Backup__MySqlBinDirectory`, `Backup__Directory`.
  Never put real credentials in `appsettings.Production.json` in source control.

Other things to revisit before a real (non-dev-machine) deployment:
- The seeded Super Admin password is randomly generated and shown once in the console at
  first run — capture it from the deployment logs, not from source.
- `Backup:Directory` and `Backup:MySqlBinDirectory` are Windows paths specific to this
  machine's MySQL install location - repoint them for any other host.
- Consider a real TLS certificate + `UseHsts`/HTTPS-only binding for anything reachable
  outside localhost (the app already calls `UseHsts()`/`UseHttpsRedirection()` in
  non-Development environments).
