using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
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

// Performance: Response Compression (Brotli + Gzip) to minimize server bandwidth & accelerate delivery
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "image/svg+xml",
        "application/javascript",
        "text/css",
        "text/html",
        "application/json"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

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
    options.Cookie.Name = ".Roven.Atelier.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
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

    // SEC-03: Account Lockout Settings against brute-force attacks
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
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
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
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
        // Auto-apply migrations only if explicitly configured (disabled by default for strict safety)
        var configuration = services.GetRequiredService<IConfiguration>();
        if (configuration.GetValue<bool>("AutoMigrateDatabase", false))
        {
            await context.Database.MigrateAsync();
        }

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
        await DbInitializer.SeedFragranceDiscoveryBaselineAsync(services);
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

// SEC-06: Security Headers Middleware (Clickjacking, MIME Sniffing, Permissions, CSP-Report-Only)
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy-Report-Only"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
        "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com https://cdn.jsdelivr.net data:; " +
        "img-src 'self' data: https://res.cloudinary.com https://*.cloudinary.com; " +
        "connect-src 'self' https://res.cloudinary.com; " +
        "frame-ancestors 'self';";

    await next();
});

app.UseResponseCompression();

// High-Performance Static Files with aggressive HTTP Cache-Control headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;

        // 1. WebP Cinematic Frames: Immutable assets - cache for 1 year in browser
        if (path.StartsWith("/frames/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        // 2. Production Static CSS, JS, Images, Libs: Cache for 30 days
        else if (path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=2592000";
        }
        else
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=86400";
        }
    }
});

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
