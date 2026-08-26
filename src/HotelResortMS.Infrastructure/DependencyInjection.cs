using HotelResortMS.Core.Entities.Identity;
using HotelResortMS.Core.Interfaces;
using HotelResortMS.Infrastructure.Data;
using HotelResortMS.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelResortMS.Infrastructure;

/// <summary>
/// Keeps Program.cs thin: one call wires the DbContext, ASP.NET Core Identity, and every
/// business service the system's later phases will also depend on.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Reasonable production defaults; System Settings can expose these as
                // configurable values in a later phase without changing this wiring.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IBusinessDateService, BusinessDateService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IFrontDeskService, FrontDeskService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPOSService, POSService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IPurchasingService, PurchasingService>();
        services.AddScoped<IAccountsPayableService, AccountsPayableService>();
        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IAccountsReceivableService, AccountsReceivableService>();
        services.AddScoped<INightAuditService, NightAuditService>();
        services.AddScoped<IDailyClosingService, DailyClosingService>();
        services.AddScoped<IHousekeepingService, HousekeepingService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddMemoryCache();
        services.AddScoped<IHotelBrandingService, HotelBrandingService>();
        services.AddHostedService<ScheduledBackupHostedService>();

        return services;
    }
}
