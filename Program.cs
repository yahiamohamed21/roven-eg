using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Extensions;
using Stockify.Data;
using Stockify.Data.Seed;
using Stockify.Models.Entities;
using Stockify.Services.Identity;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// Configure Anti-Forgery to support header-based validation for AJAX / Fetch requests
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Add Distributed Cache & Session support for Shopping Cart
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 2. Configure Database Context (Entity Framework Core with SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 3. Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

// 4. Configure Application Cookie (Authentication / Authorization redirects)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// 5. Register Domain Services via Dependency Injection
builder.Services.AddApplicationServices();

var app = builder.Build();

// 6. Seed Roles & Default Admin (Only runs if the database exists and can be reached)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        if (context.Database.CanConnect())
        {
            await DbInitializer.SeedRolesAndAdminAsync(services);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Initial seeding skipped: database does not exist or has not been migrated yet.");
    }
}

// 7. Configure HTTP Request Pipeline & Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// Storefront Website Visits Analytics Tracker
app.UseMiddleware<Stockify.Services.Middleware.StorefrontVisitTrackerMiddleware>();

// Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// 8. Configure Route Mapping (Storefront is default landing page)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
