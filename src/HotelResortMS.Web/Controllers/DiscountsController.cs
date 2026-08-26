using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Sales;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 17/18: configurable discount definitions (Senior Citizen, PWD,
/// Promotional, Corporate, Membership, Other). DiscountService is the only code path that
/// reads these to actually apply a discount - this controller only maintains the catalog.</summary>
[RequirePermission(SystemModules.Discounts, PermissionAction.View)]
public class DiscountsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public DiscountsController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var discounts = await _db.Discounts.OrderBy(d => d.Type).ThenBy(d => d.Name).ToListAsync();
        return View(discounts);
    }

    [RequirePermission(SystemModules.Discounts, PermissionAction.Add)]
    public IActionResult Create() => View(new DiscountEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Discounts, PermissionAction.Add)]
    public async Task<IActionResult> Create(DiscountEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var entity = new Discount
        {
            Name = model.Name,
            Type = model.Type,
            CalculationType = model.CalculationType,
            Percentage = model.Percentage,
            FixedAmount = model.FixedAmount,
            EligibleForRooms = model.EligibleForRooms,
            EligibleForAmenities = model.EligibleForAmenities,
            EligibleForProducts = model.EligibleForProducts,
            EligibleForServices = model.EligibleForServices,
            EffectiveDate = model.EffectiveDate,
            ExpirationDate = model.ExpirationDate,
            RequiresIdVerification = model.RequiresIdVerification,
            CreatedBy = User.Identity?.Name
        };
        _db.Discounts.Add(entity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Discounts, "Create", entity.Id.ToString(), newValues: new { entity.Name, entity.Type, entity.Percentage });
        TempData["Success"] = "Discount created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(SystemModules.Discounts, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.Discounts.FindAsync(id);
        if (entity is null) return NotFound();

        return View(new DiscountEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Type = entity.Type,
            CalculationType = entity.CalculationType,
            Percentage = entity.Percentage,
            FixedAmount = entity.FixedAmount,
            EligibleForRooms = entity.EligibleForRooms,
            EligibleForAmenities = entity.EligibleForAmenities,
            EligibleForProducts = entity.EligibleForProducts,
            EligibleForServices = entity.EligibleForServices,
            EffectiveDate = entity.EffectiveDate,
            ExpirationDate = entity.ExpirationDate,
            RequiresIdVerification = entity.RequiresIdVerification,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Discounts, PermissionAction.Edit)]
    public async Task<IActionResult> Edit(DiscountEditViewModel model)
    {
        var entity = await _db.Discounts.FindAsync(model.Id);
        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        // Section 15: editing a discount's percentage never rewrites the DiscountTransaction
        // rows already recorded against past sales - those keep the rate actually applied.
        var oldValues = new { entity.Percentage, entity.FixedAmount, entity.IsActive };

        entity.Name = model.Name;
        entity.Type = model.Type;
        entity.CalculationType = model.CalculationType;
        entity.Percentage = model.Percentage;
        entity.FixedAmount = model.FixedAmount;
        entity.EligibleForRooms = model.EligibleForRooms;
        entity.EligibleForAmenities = model.EligibleForAmenities;
        entity.EligibleForProducts = model.EligibleForProducts;
        entity.EligibleForServices = model.EligibleForServices;
        entity.EffectiveDate = model.EffectiveDate;
        entity.ExpirationDate = model.ExpirationDate;
        entity.RequiresIdVerification = model.RequiresIdVerification;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(SystemModules.Discounts, "Update", entity.Id.ToString(), oldValues,
            new { entity.Percentage, entity.FixedAmount, entity.IsActive });
        TempData["Success"] = "Discount updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Discounts, PermissionAction.Delete)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var entity = await _db.Discounts.FindAsync(id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Discounts, "Deactivate", id.ToString());

        TempData["Success"] = "Discount deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // Real delete: blocked if the discount has ever actually been applied to a transaction.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Discounts, PermissionAction.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Discounts.FindAsync(id);
        if (entity is null) return NotFound();

        var hasHistory = await _db.DiscountTransactions.AnyAsync(t => t.DiscountId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This discount cannot be deleted because it has been applied to past transactions. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Discounts.Remove(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Discounts, "Delete", id.ToString(), oldValues: new { entity.Name });

        TempData["Success"] = "Discount deleted.";
        return RedirectToAction(nameof(Index));
    }
}
