using HotelResortMS.Infrastructure;
using HotelResortMS.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed roles/Super Admin/system settings on every startup - idempotent, and this is the
// only place the generated Super Admin password is ever visible (Section 45/49; see
// DbSeeder for why it is never stored anywhere after this).
using (var scope = app.Services.CreateScope())
{
    var seedResult = await DbSeeder.SeedAsync(scope.ServiceProvider);
    if (seedResult.WasNewlyCreated)
    {
        Console.WriteLine();
        Console.WriteLine("==================================================================");
        Console.WriteLine(" Super Admin account created");
        Console.WriteLine($"   Email:    {seedResult.SuperAdminEmail}");
        Console.WriteLine($"   Password: {seedResult.SuperAdminPassword}");
        Console.WriteLine(" Save this password now - it will not be shown again.");
        Console.WriteLine("==================================================================");
        Console.WriteLine();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
