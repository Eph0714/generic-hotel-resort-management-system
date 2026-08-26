using System.Security.Cryptography;
using HotelResortMS.Core.Common;
using HotelResortMS.Core.Entities;
using HotelResortMS.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelResortMS.Infrastructure.Data;

/// <summary>
/// Runs once at startup to guarantee the system is usable on first boot: the built-in
/// roles (Section 45), a Super Admin account with full permissions on every module, and
/// a handful of starter System Settings (Section 49). Every step is idempotent so it is
/// safe to run on every application start.
/// </summary>
public static class DbSeeder
{
    /// <summary>Result of seeding, surfaced so the host can print the generated
    /// credentials to the console/log on first run (per the "always show login after
    /// build" convention) - the password is never stored anywhere after this.</summary>
    public record SeedResult(string? SuperAdminEmail, string? SuperAdminPassword, bool WasNewlyCreated);

    public static async Task<SeedResult> SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync();

        foreach (var roleName in SystemRoles.All)
        {
            if (await roleManager.FindByNameAsync(roleName) is null)
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName)
                {
                    IsSystemRole = true,
                    Description = $"Built-in system role: {roleName}"
                });
            }
        }

        string? generatedPassword = null;
        string superAdminEmail = "admin@hotel.local";
        var wasNewlyCreated = false;

        var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
        if (superAdmin is null)
        {
            // Generate a strong random password rather than a hard-coded default - it is
            // reported to the operator once here and never persisted in plain text again.
            generatedPassword = GenerateStrongPassword();

            superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true,
                CreatedBy = "System"
            };

            var createResult = await userManager.CreateAsync(superAdmin, generatedPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to seed Super Admin account: {Errors}", errors);
                throw new InvalidOperationException($"Failed to seed Super Admin account: {errors}");
            }

            await userManager.AddToRoleAsync(superAdmin, SystemRoles.SuperAdmin);
            wasNewlyCreated = true;
        }

        // Grant Super Admin full permission flags across every module. This is
        // belt-and-suspenders on top of PermissionService's role-name short-circuit: it
        // also makes the permission matrix screen show Super Admin's grants explicitly
        // rather than as a hidden special case.
        var superAdminRole = await roleManager.FindByNameAsync(SystemRoles.SuperAdmin);
        if (superAdminRole is not null)
        {
            foreach (var module in SystemModules.All)
            {
                var exists = await db.RolePermissions
                    .AnyAsync(p => p.RoleId == superAdminRole.Id && p.Module == module);
                if (!exists)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = superAdminRole.Id,
                        Module = module,
                        CanView = true,
                        CanAdd = true,
                        CanEdit = true,
                        CanDelete = true,
                        CanApprove = true,
                        CanVoid = true,
                        CanRefund = true,
                        CanPrint = true,
                        CanExport = true,
                        CanConfigure = true
                    });
                }
            }
        }

        // Starter System Settings (Section 49) - placeholders, never hard-coded into
        // code; administrators edit these through the Settings screen.
        await SeedSettingIfMissingAsync(db, "Hotel.Name", "Sample Hotel and Resort", "Hotel/Resort Setup", "Displayed hotel/resort name across the system.");
        await SeedSettingIfMissingAsync(db, "Hotel.Currency", "PHP", "Hotel/Resort Setup", "Default currency code.");
        await SeedSettingIfMissingAsync(db, "Hotel.LogoPath", "", "Branding & Theme", "Logo shown in the sidebar and beside the Dashboard banner - uploaded below, not edited as text.");
        await SeedSettingIfMissingAsync(db, "Hotel.BannerMode", HotelResortMS.Core.Interfaces.SupportedBannerModes.Image, "Branding & Theme", "Whether the Dashboard banner shows an uploaded image or a plain color fill.");
        await SeedSettingIfMissingAsync(db, "Hotel.BannerPath", "", "Branding & Theme", "Banner image shown at the top of the Dashboard - uploaded below, not edited as text. Only used when Banner Display is Image.");
        await SeedSettingIfMissingAsync(db, "Hotel.BannerColor", "#2f6fed", "Branding & Theme", "Banner color fill, used when Banner Display is Color.");
        await SeedSettingIfMissingAsync(db, "Hotel.BannerText", "", "Branding & Theme", "Optional caption text shown over the Dashboard banner.");
        await SeedSettingIfMissingAsync(db, "Hotel.BannerAlignment", HotelResortMS.Core.Interfaces.SupportedBannerAlignments.Middle, "Branding & Theme", "Where the logo and caption sit within the Dashboard banner (Left, Middle, or Right).");
        await SeedSettingIfMissingAsync(db, "App.Theme", HotelResortMS.Core.Interfaces.SupportedThemes.Light, "Branding & Theme", "Application color theme (Light or Dark).");
        await SeedSettingIfMissingAsync(db, "App.ThemeColor", HotelResortMS.Core.Interfaces.SupportedThemeColors.DefaultHex, "Branding & Theme", "Accent color used for every button and the sidebar's active-link highlight, app-wide.");
        await SeedSettingIfMissingAsync(db, "Finance.VatRate", "12", "Finance", "Default VAT percentage applied to taxable sales.");
        await SeedSettingIfMissingAsync(db, "Finance.ServiceChargeRate", "0", "Finance", "Default service charge percentage.");
        await SeedSettingIfMissingAsync(db, "Discount.SeniorCitizenPercent", "20", "Discounts", "Senior Citizen discount percentage per current Philippine regulations - update if the law changes.");
        await SeedSettingIfMissingAsync(db, "Discount.PwdPercent", "20", "Discounts", "PWD discount percentage per current Philippine regulations - update if the law changes.");
        await SeedSettingIfMissingAsync(db, "Backup.ScheduleEnabled", "false", "Backup", "Whether ScheduledBackupHostedService runs an automatic daily backup (Section 50).");
        await SeedSettingIfMissingAsync(db, "Backup.ScheduleTimeOfDay", "02:00", "Backup", "Time of day (24h, server local time) the automatic daily backup runs, e.g. 02:00.");

        await db.SaveChangesAsync();

        await SeedHotelOperationsSampleDataAsync(db);

        return new SeedResult(wasNewlyCreated ? superAdminEmail : null, generatedPassword, wasNewlyCreated);
    }

    /// <summary>
    /// Section 6/13/14 starter master data so Phase 2 (Rooms/Amenities/Reservations) is
    /// immediately usable on a fresh install rather than an empty shell. All of this is
    /// ordinary editable master data through the CRUD screens - nothing here is hard-coded
    /// into business logic (Section 49).
    /// </summary>
    private static async Task SeedHotelOperationsSampleDataAsync(ApplicationDbContext db)
    {
        if (!await db.BedTypes.AnyAsync())
        {
            db.BedTypes.AddRange(
                new BedType { Name = "Single" },
                new BedType { Name = "Queen" },
                new BedType { Name = "King" },
                new BedType { Name = "Twin" });
        }

        if (!await db.FloorAreas.AnyAsync())
        {
            db.FloorAreas.AddRange(
                new FloorArea { Name = "Ground Floor" },
                new FloorArea { Name = "2nd Floor" },
                new FloorArea { Name = "3rd Floor" },
                new FloorArea { Name = "Garden Wing" });
        }

        if (!await db.AmenityCategories.AnyAsync())
        {
            db.AmenityCategories.AddRange(
                new AmenityCategory { Name = "Pool & Recreation" },
                new AmenityCategory { Name = "Function Halls" },
                new AmenityCategory { Name = "Cottages" });
        }

        await db.SaveChangesAsync();

        if (!await db.RoomTypes.AnyAsync())
        {
            var standard = new RoomType { Name = "Standard", BaseCapacity = 2, RegularRate = 1500, WeekendRate = 1800, HolidayRate = 2200, SeasonalRate = 2000, ExtraPersonRate = 300 };
            var deluxe = new RoomType { Name = "Deluxe", BaseCapacity = 3, RegularRate = 2500, WeekendRate = 2900, HolidayRate = 3500, SeasonalRate = 3200, ExtraPersonRate = 400 };
            var suite = new RoomType { Name = "Suite", BaseCapacity = 4, RegularRate = 4500, WeekendRate = 5200, HolidayRate = 6000, SeasonalRate = 5500, ExtraPersonRate = 500 };
            db.RoomTypes.AddRange(standard, deluxe, suite);
            await db.SaveChangesAsync();

            var queen = await db.BedTypes.FirstAsync(b => b.Name == "Queen");
            var king = await db.BedTypes.FirstAsync(b => b.Name == "King");
            var ground = await db.FloorAreas.FirstAsync(f => f.Name == "Ground Floor");
            var second = await db.FloorAreas.FirstAsync(f => f.Name == "2nd Floor");

            db.Rooms.AddRange(
                new Room { RoomNumber = "101", RoomTypeId = standard.Id, BedTypeId = queen.Id, FloorAreaId = ground.Id, Capacity = 2 },
                new Room { RoomNumber = "102", RoomTypeId = standard.Id, BedTypeId = queen.Id, FloorAreaId = ground.Id, Capacity = 2 },
                new Room { RoomNumber = "201", RoomTypeId = deluxe.Id, BedTypeId = king.Id, FloorAreaId = second.Id, Capacity = 3 },
                new Room { RoomNumber = "202", RoomTypeId = deluxe.Id, BedTypeId = king.Id, FloorAreaId = second.Id, Capacity = 3 },
                new Room { RoomNumber = "301", RoomTypeId = suite.Id, BedTypeId = king.Id, FloorAreaId = second.Id, Capacity = 4 });
        }

        if (!await db.Amenities.AnyAsync())
        {
            var pool = await db.AmenityCategories.FirstAsync(c => c.Name == "Pool & Recreation");
            var hall = await db.AmenityCategories.FirstAsync(c => c.Name == "Function Halls");
            var cottage = await db.AmenityCategories.FirstAsync(c => c.Name == "Cottages");

            db.Amenities.AddRange(
                new Amenity { Name = "Main Swimming Pool", AmenityCategoryId = pool.Id, Capacity = 50, HourlyRate = 200, DailyRate = 1500, RegularRate = 1500, WeekendRate = 1800, HolidayRate = 2200, SeasonalRate = 2000, MinimumHours = 2 },
                new Amenity { Name = "Grand Function Hall", AmenityCategoryId = hall.Id, Capacity = 150, HourlyRate = 1000, DailyRate = 8000, RegularRate = 8000, WeekendRate = 9500, HolidayRate = 12000, SeasonalRate = 10000, MinimumHours = 4 },
                new Amenity { Name = "Garden Cottage A", AmenityCategoryId = cottage.Id, Capacity = 10, HourlyRate = 150, DailyRate = 1000, RegularRate = 1000, WeekendRate = 1200, HolidayRate = 1500, SeasonalRate = 1300, MinimumHours = 2 });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSettingIfMissingAsync(ApplicationDbContext db, string key, string value, string category, string description)
    {
        if (!await db.SystemSettings.AnyAsync(s => s.Key == key))
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                Category = category,
                Description = description
            });
        }
    }

    private static string GenerateStrongPassword()
    {
        // Guarantees at least one of each required character class so it always satisfies
        // ASP.NET Core Identity's default password policy, then fills the remainder randomly.
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*";
        const string all = upper + lower + digits + special;

        Span<char> pwd = stackalloc char[16];
        pwd[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        pwd[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        pwd[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        pwd[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
        for (var i = 4; i < pwd.Length; i++)
        {
            pwd[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Shuffle so the guaranteed classes aren't always in the same positions.
        for (var i = pwd.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (pwd[i], pwd[j]) = (pwd[j], pwd[i]);
        }

        return new string(pwd);
    }
}
