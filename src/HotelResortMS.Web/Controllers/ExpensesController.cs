using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Web.Models.Finance;
using HotelResortMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelResortMS.Web.Controllers;

/// <summary>Section 39: cash/business outlays. A posted expense is Voided, never deleted.</summary>
[RequirePermission(SystemModules.Expenses, PermissionAction.View)]
public class ExpensesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IExpenseService _expenseService;

    public ExpensesController(ApplicationDbContext db, IExpenseService expenseService)
    {
        _db = db;
        _expenseService = expenseService;
    }

    public async Task<IActionResult> Index()
    {
        var expenses = await _db.Expenses.OrderByDescending(x => x.ActualDateTime).ToListAsync();
        return View(expenses);
    }

    [RequirePermission(SystemModules.Expenses, PermissionAction.Add)]
    public IActionResult Create() => View(new ExpenseCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Expenses, PermissionAction.Add)]
    public async Task<IActionResult> Create(ExpenseCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var expense = await _expenseService.RecordExpenseAsync(
                model.Category, model.Description, model.Amount, model.PaymentMethod,
                model.Payee, model.Reference, User.Identity?.Name ?? "Unknown", model.Remarks);

            TempData["Success"] = $"Expense {expense.ExpenseNumber} recorded.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(SystemModules.Expenses, PermissionAction.Void)]
    public async Task<IActionResult> Void(int id, string reason)
    {
        try
        {
            await _expenseService.VoidExpenseAsync(id, User.Identity?.Name ?? "Unknown", reason);
            TempData["Success"] = "Expense voided.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
