using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stockify.Common.Extensions;
using Stockify.Data;
using Stockify.Data.Seed;
using Stockify.Models.Entities;
using Stockify.Services.Identity;
using Stockify.Services.Implementations;
using Stockify.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Load optional local secrets configuration (ignored in source control)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// Configure Anti-Forgery to support header-based validation for AJAX / Fetch requests
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Add Memory Cache & Session support for Shopping Cart & Storefront Caching
builder.Services.AddMemoryCache();
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
{
    options.UseSqlServer(connectionString);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

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
    options.Cookie.Name = ".Roven.Atelier.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;

    options.Events.OnSigningIn = context =>
    {
        if (context.Properties.IsPersistent)
        {
            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
            context.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30);
            context.CookieOptions.MaxAge = TimeSpan.FromDays(30);
        }
        else
        {
            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8);
            context.CookieOptions.Expires = null;
            context.CookieOptions.MaxAge = null;
        }
        return Task.CompletedTask;
    };
});

// 5. Register Domain Services via Dependency Injection
builder.Services.AddApplicationServices();

var app = builder.Build();

// Configure Forwarded Headers for reverse proxy (Nginx)
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
#if NET10_0_OR_GREATER
forwardedOptions.KnownIPNetworks.Clear();
#else
forwardedOptions.KnownNetworks.Clear();
#endif
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// 6. Migrate Database & Seed Roles / Initial Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        // Auto-apply migrations if pending
        await context.Database.MigrateAsync();

        // Ensure Comparison Section settings are initialized with active status
        var homeSetting = await context.HomeSettings.FirstOrDefaultAsync();
        if (homeSetting != null)
        {
            bool modified = false;
            if (string.IsNullOrWhiteSpace(homeSetting.ComparisonTitle) || (!homeSetting.ComparisonActive && homeSetting.OrderComparison <= 0))
            {
                homeSetting.ComparisonTitle = "US VS THEM";
                homeSetting.ComparisonBadgeText = "MAISON ROVEN • QUALITY PROMISE";
                homeSetting.ComparisonSubtitle = "Why settle for the brand name when you can wear the same scent for a fraction of the price?";
                homeSetting.ComparisonButtonText = "Shop Now";
                homeSetting.ComparisonButtonLink = "/Store";
                homeSetting.ComparisonBeforeLabel = "Before";
                homeSetting.ComparisonAfterLabel = "After";
                homeSetting.ComparisonBeforeImageUrl = "/images/comparison/roven-comparison-before.svg";
                homeSetting.ComparisonAfterImageUrl = "/images/comparison/roven-comparison-after.svg";
                homeSetting.ComparisonActive = true;
                homeSetting.ShowComparisonSection = true;
                homeSetting.OrderComparison = 6;
                modified = true;
            }
            if (modified)
            {
                await context.SaveChangesAsync();
            }
        }

        await DbInitializer.SeedRolesAndAdminAsync(services);
        await DbInitializer.AutoSyncImagesToCloudinaryAsync(services);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// 7. Configure HTTP Request Pipeline & Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// Storefront Website Visits Analytics Tracker
app.UseMiddleware<Stockify.Services.Middleware.StorefrontVisitTrackerMiddleware>();

// Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Lightweight Health Check for deployment pipelines
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// 8. Configure Route Mapping (Storefront is default landing page)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
