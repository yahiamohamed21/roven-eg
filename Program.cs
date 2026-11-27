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
        // Automatically apply pending database migrations to ensure database schema matches code
        var configuration = services.GetRequiredService<IConfiguration>();
        if (configuration.GetValue<bool>("AutoMigrateDatabase", true))
        {
            logger.LogInformation("Verifying and applying any pending database migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }

        // Ensure ProductCategories table exists and is populated from existing Product.CategoryId
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductCategories')
                BEGIN
                    CREATE TABLE [ProductCategories] (
                        [ProductId] int NOT NULL,
                        [CategoryId] int NOT NULL,
                        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([ProductId], [CategoryId]),
                        CONSTRAINT [FK_ProductCategories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_ProductCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_ProductCategories_CategoryId] ON [ProductCategories] ([CategoryId]);
                END;

                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductCategories') AND EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
                BEGIN
                    INSERT INTO [ProductCategories] ([ProductId], [CategoryId])
                    SELECT p.[Id], p.[CategoryId]
                    FROM [Products] p
                    WHERE p.[CategoryId] > 0
                      AND NOT EXISTS (
                          SELECT 1 FROM [ProductCategories] pc 
                          WHERE pc.[ProductId] = p.[Id] AND pc.[CategoryId] = p.[CategoryId]
                      );
                END;
            ");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ProductCategories table verification check: {Message}", ex.Message);
        }

        // Ensure HomeOffers table has ThirdProductId column for 3-perfume bundle offers
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('HomeOffers') AND name = 'ThirdProductId')
                BEGIN
                    ALTER TABLE [HomeOffers] ADD [ThirdProductId] int NULL;
                    ALTER TABLE [HomeOffers] ADD CONSTRAINT [FK_HomeOffers_Products_ThirdProductId] FOREIGN KEY ([ThirdProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION;
                    CREATE INDEX [IX_HomeOffers_ThirdProductId] ON [HomeOffers] ([ThirdProductId]);
                END;

                -- Clear any artificial/dummy CustomImageUrl values so the real product perfume images are used
                UPDATE [HomeOffers] 
                SET [CustomImageUrl] = NULL 
                WHERE [CustomImageUrl] IS NOT NULL 
                  AND ([CustomImageUrl] LIKE '/roven/products/%' 
                    OR [CustomImageUrl] LIKE '%star_master_offer%' 
                    OR [CustomImageUrl] LIKE '%placeholder%');
            ");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HomeOffers.ThirdProductId column check: {Message}", ex.Message);
        }

        // Ensure ProductSizes table and Cost/Profit columns on Products & OrderItems exist (separate batches so SQL Server compiles new columns cleanly)
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'CostPrice')
                    ALTER TABLE [Products] ADD [CostPrice] decimal(18,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ProfitAmount')
                    ALTER TABLE [Products] ADD [ProfitAmount] decimal(18,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'ProductSizeId')
                    ALTER TABLE [OrderItems] ADD [ProductSizeId] int NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'SizeLabel')
                    ALTER TABLE [OrderItems] ADD [SizeLabel] nvarchar(40) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'UnitCost')
                    ALTER TABLE [OrderItems] ADD [UnitCost] decimal(18,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'UnitProfit')
                    ALTER TABLE [OrderItems] ADD [UnitProfit] decimal(18,2) NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'IsTester')
                    ALTER TABLE [Products] ADD [IsTester] bit NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'TesterRequiredCount')
                    ALTER TABLE [Products] ADD [TesterRequiredCount] int NOT NULL DEFAULT 4;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'DisplayOrder')
                    ALTER TABLE [Products] ADD [DisplayOrder] int NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OrderItems') AND name = 'SelectedTestersSummary')
                    ALTER TABLE [OrderItems] ADD [SelectedTestersSummary] nvarchar(600) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('HomeSettings') AND name = 'ComparisonFeature1Title')
                    ALTER TABLE [HomeSettings] ADD
                        [ComparisonFeature1Title] nvarchar(200) NOT NULL DEFAULT N'Extrait de Parfum (30% Concentration)',
                        [ComparisonFeature1Desc] nvarchar(350) NOT NULL DEFAULT N'أعلى تركيز زيوت عطرية نقية معتمدة لثبات وفوحان فائق',
                        [ComparisonFeature1MetricVal] nvarchar(40) NOT NULL DEFAULT N'30%',
                        [ComparisonFeature1MetricLbl] nvarchar(40) NOT NULL DEFAULT N'Pure',
                        [ComparisonFeature1Icon] nvarchar(100) NOT NULL DEFAULT N'fa-solid fa-droplet',
                        [ComparisonFeature2Title] nvarchar(200) NOT NULL DEFAULT N'24+ Hours Royal Longevity & Sillage',
                        [ComparisonFeature2Desc] nvarchar(350) NOT NULL DEFAULT N'ثبات وفوحان ملكي يدوم أكثر من 24 ساعة على الملابس',
                        [ComparisonFeature2MetricVal] nvarchar(40) NOT NULL DEFAULT N'24h+',
                        [ComparisonFeature2MetricLbl] nvarchar(40) NOT NULL DEFAULT N'Long',
                        [ComparisonFeature2Icon] nvarchar(100) NOT NULL DEFAULT N'fa-solid fa-hourglass-half',
                        [ComparisonFeature3Title] nvarchar(200) NOT NULL DEFAULT N'100% Risk-Free Inspection on Delivery',
                        [ComparisonFeature3Desc] nvarchar(350) NOT NULL DEFAULT N'حق المعاينة والتجربة الكاملة قبل استلام الطرد والدفع',
                        [ComparisonFeature3MetricVal] nvarchar(40) NOT NULL DEFAULT N'100%',
                        [ComparisonFeature3MetricLbl] nvarchar(40) NOT NULL DEFAULT N'COD',
                        [ComparisonFeature3Icon] nvarchar(100) NOT NULL DEFAULT N'fa-solid fa-shield-heart';

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductTesterOptions')
                BEGIN
                    CREATE TABLE [ProductTesterOptions] (
                        [TesterProductId] int NOT NULL,
                        [FragranceProductId] int NOT NULL,
                        [DisplayOrder] int NOT NULL DEFAULT 0,
                        CONSTRAINT [PK_ProductTesterOptions] PRIMARY KEY ([TesterProductId], [FragranceProductId]),
                        CONSTRAINT [FK_ProductTesterOptions_Products_TesterProductId] FOREIGN KEY ([TesterProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_ProductTesterOptions_Products_FragranceProductId] FOREIGN KEY ([FragranceProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
                    );
                    CREATE INDEX [IX_ProductTesterOptions_FragranceProductId] ON [ProductTesterOptions] ([FragranceProductId]);
                END;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductSizes')
                BEGIN
                    CREATE TABLE [ProductSizes] (
                        [Id] int NOT NULL IDENTITY,
                        [ProductId] int NOT NULL,
                        [SizeLabel] nvarchar(40) NOT NULL,
                        [CostPrice] decimal(18,2) NOT NULL DEFAULT 0,
                        [ProfitAmount] decimal(18,2) NOT NULL DEFAULT 0,
                        [SellingPrice] decimal(18,2) NOT NULL DEFAULT 0,
                        [IsDefault] bit NOT NULL DEFAULT 0,
                        [DisplayOrder] int NOT NULL DEFAULT 0,
                        CONSTRAINT [PK_ProductSizes] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ProductSizes_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_ProductSizes_ProductId] ON [ProductSizes] ([ProductId]);
                END;
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                -- Backfill default ProfitAmount for legacy products that have Price > 0 and no Cost/Profit set
                UPDATE [Products]
                SET [ProfitAmount] = [Price]
                WHERE [CostPrice] = 0 AND [ProfitAmount] = 0 AND [Price] > 0;

                -- Backfill initial DisplayOrder for existing products if not yet assigned
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'DisplayOrder')
                   AND NOT EXISTS (SELECT 1 FROM [Products] WHERE [DisplayOrder] <> 0)
                BEGIN
                    ;WITH OrderedProducts AS (
                        SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Name] ASC, [Id] ASC) AS rn
                        FROM [Products]
                    )
                    UPDATE p
                    SET [DisplayOrder] = o.rn
                    FROM [Products] p
                    INNER JOIN OrderedProducts o ON p.[Id] = o.[Id];
                END;

                -- Seed default 100ml size row for any existing product missing ProductSizes
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductSizes') AND EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
                BEGIN
                    INSERT INTO [ProductSizes] ([ProductId], [SizeLabel], [CostPrice], [ProfitAmount], [SellingPrice], [IsDefault], [DisplayOrder])
                    SELECT p.[Id], N'100ml', p.[CostPrice], p.[ProfitAmount], p.[Price], 1, 0
                    FROM [Products] p
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [ProductSizes] ps WHERE ps.[ProductId] = p.[Id]
                    );
                END;
            ");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductSizes & Cost/Profit schema initialization: {Message}", ex.Message);
        }

        // Deduplicate any historical duplicate unrecovered AbandonedCheckouts rows
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                ;WITH DupPhone AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([CustomerPhone])), ' ', ''), '-', ''), '(', ''), ')', '')
                               ORDER BY [LastUpdatedAt] DESC, [Id] DESC
                           ) AS rn
                    FROM [AbandonedCheckouts]
                    WHERE [IsRecovered] = 0 AND [CustomerPhone] IS NOT NULL AND LTRIM(RTRIM([CustomerPhone])) <> N''
                )
                DELETE FROM DupPhone WHERE rn > 1;

                ;WITH DupSession AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [SessionId]
                               ORDER BY [LastUpdatedAt] DESC, [Id] DESC
                           ) AS rn
                    FROM [AbandonedCheckouts]
                    WHERE [IsRecovered] = 0 AND [SessionId] IS NOT NULL AND LTRIM(RTRIM([SessionId])) <> N''
                )
                DELETE FROM DupSession WHERE rn > 1;
            ");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AbandonedCheckouts deduplication cleanup: {Message}", ex.Message);
        }

        // Ensure Web Push Subscriptions & VAPID Settings tables and AbandonedCheckouts.AdminPushSentAt exist
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[PushSubscriptions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [PushSubscriptions] (
                        [Id] int NOT NULL IDENTITY(1,1) PRIMARY KEY,
                        [Endpoint] nvarchar(1024) NOT NULL,
                        [P256dh] nvarchar(512) NOT NULL,
                        [Auth] nvarchar(512) NOT NULL,
                        [UserId] nvarchar(256) NULL,
                        [UserAgent] nvarchar(512) NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [LastSuccessAt] datetime2 NULL
                    );
                END;

                IF OBJECT_ID(N'[PushVapidSettings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [PushVapidSettings] (
                        [Id] int NOT NULL IDENTITY(1,1) PRIMARY KEY,
                        [Subject] nvarchar(256) NOT NULL,
                        [PublicKey] nvarchar(512) NOT NULL,
                        [PrivateKey] nvarchar(512) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF COL_LENGTH(N'[AbandonedCheckouts]', N'AdminPushSentAt') IS NULL
                BEGIN
                    ALTER TABLE [AbandonedCheckouts] ADD [AdminPushSentAt] datetime2 NULL;
                END;
            ");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Web Push schema initialization: {Message}", ex.Message);
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
        await DbInitializer.EnsureProductImagesWebpAsync(services);
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
