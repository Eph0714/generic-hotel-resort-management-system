using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Inventory;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 34 (Recipe/BOM): links a product that tracks inventory to the
/// InventoryItems consumed when it sells - IInventoryService.DeductForSaleAsync reads
/// exactly what this screen writes.</summary>
[RequirePermission(SystemModules.Inventory, PermissionAction.View)]
public class RecipesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public RecipesController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _db.Products
            .Where(p => p.TrackInventory)
            .Select(p => new
            {
                p.Id,
                p.Name,
                HasRecipe = _db.Recipes.Any(r => r.ProductId == p.Id && r.IsActive)
            })
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.Products = products.Select(p => (p.Id, p.Name, p.HasRecipe)).ToList();
        return View();
    }

    [RequirePermission(SystemModules.Inventory, PermissionAction.Configure)]
    public async Task<IActionResult> Edit(int productId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound();

        var recipe = await _db.Recipes.Include(r => r.Components).FirstOrDefaultAsync(r => r.ProductId == productId);

        var model = new RecipeEditViewModel
        {
            ProductId = productId,
            ProductName = product.Name,
            Notes = recipe?.Notes,
            Components = recipe?.Components.Select(c => new RecipeComponentInput { InventoryItemId = c.InventoryItemId, QuantityRequired = c.QuantityRequired }).ToList() ?? new(),
            AvailableItems = await _db.InventoryItems.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Configure)]
    public async Task<IActionResult> Edit(int productId, int[] itemId, decimal[] quantity, string? notes)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null) return NotFound();

        var recipe = await _db.Recipes.Include(r => r.Components).FirstOrDefaultAsync(r => r.ProductId == productId);
        if (recipe is null)
        {
            recipe = new Recipe { ProductId = productId, CreatedBy = User.Identity?.Name };
            _db.Recipes.Add(recipe);
        }
        else
        {
            // Section 15: replacing the component list only changes what future sales
            // deduct - InventoryTransaction rows already posted for past sales are untouched.
            _db.RecipeDetails.RemoveRange(recipe.Components);
            recipe.Components.Clear();
        }

        recipe.Notes = notes;
        recipe.UpdatedAt = DateTime.UtcNow;
        recipe.UpdatedBy = User.Identity?.Name;

        var pairs = itemId.Zip(quantity, (id, qty) => (id, qty)).Where(p => p.qty > 0);
        foreach (var (id, qty) in pairs)
        {
            recipe.Components.Add(new RecipeDetail { InventoryItemId = id, QuantityRequired = qty });
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(SystemModules.Inventory, "SaveRecipe", productId.ToString(), newValues: new { product.Name, ComponentCount = recipe.Components.Count });

        TempData["Success"] = $"Recipe saved for {product.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Inventory, PermissionAction.Delete)]
    public async Task<IActionResult> Remove(int productId)
    {
        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.ProductId == productId);
        if (recipe is not null)
        {
            recipe.IsActive = false;
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(SystemModules.Inventory, "RemoveRecipe", productId.ToString());
        }
        TempData["Success"] = "Recipe removed.";
        return RedirectToAction(nameof(Index));
    }
}
