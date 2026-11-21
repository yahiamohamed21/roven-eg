using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stockify.Data;
using Stockify.DTOs.CatalogSync;
using Stockify.Models.Entities;
using Stockify.Models.Entities.Sync;
using Stockify.Services.Sync;

namespace Stockify.Controllers.Api;

[ApiController]
[Route("api/sync")]
public class SyncApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncApiController> _logger;
    private readonly IWebHostEnvironment _environment;

    public SyncApiController(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<SyncApiController> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    private bool ValidateApiKey()
    {
        var configuredKey = _configuration["CatalogSync:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            _logger.LogError("Synchronization API request rejected: 'CatalogSync:ApiKey' is not configured.");
            return false;
        }

        if (!Request.Headers.TryGetValue("X-Sync-Api-Key", out var providedKeyValues))
        {
            return false;
        }

        var providedKey = providedKeyValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        byte[] configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        byte[] providedBytes = Encoding.UTF8.GetBytes(providedKey);

        if (configuredBytes.Length != providedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        if (!ValidateApiKey())
        {
            return Unauthorized(new { success = false, message = "Invalid or missing X-Sync-Api-Key header." });
        }

        var lastCursor = _context.SyncChangeFeedEvents.OrderByDescending(c => c.Cursor).Select(c => c.Cursor).FirstOrDefault();
        return Ok(new
        {
            success = true,
            status = "Online",
            serverTimeUtc = DateTime.UtcNow,
            currentCursor = lastCursor,
            environment = _environment.EnvironmentName
        });
    }

    [HttpGet("verify-outbox-pipeline")]
    public async Task<IActionResult> VerifyOutboxPipeline()
    {
        if (!ValidateApiKey())
        {
            return Unauthorized(new { success = false, message = "Invalid or missing X-Sync-Api-Key header." });
        }

        var results = new Dictionary<string, bool>();

        // 1. Cleanup any prior test artifacts
        var existingTestEvents = await _context.SyncOutboxEvents
            .Where(e => e.EntitySyncKey == "VERIFY-SKU-001" || e.EntitySyncKey == "VERIFY-CAT-001")
            .ToListAsync();
        _context.SyncOutboxEvents.RemoveRange(existingTestEvents);

        var existingP = await _context.Products.FirstOrDefaultAsync(p => p.Sku == "VERIFY-SKU-001");
        if (existingP != null) _context.Products.Remove(existingP);

        var existingC = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "VERIFY-CAT-001");
        if (existingC != null) _context.Categories.Remove(existingC);

        await _context.SaveChangesAsync();

        var baseCategory = await _context.Categories.FirstAsync();

        // 2. Test Scenario 4: Product Creation outbox event
        var testProduct = new Product
        {
            Sku = "VERIFY-SKU-001",
            Name = "Verification Perfume",
            Description = "Commercial outbox test",
            Price = 1450.00m,
            StockQuantity = 15,
            CategoryId = baseCategory.Id
        };
        _context.Products.Add(testProduct);
        await _context.SaveChangesAsync();

        var outboxCreate = await _context.SyncOutboxEvents
            .FirstOrDefaultAsync(e => e.EntityType == "Product" && e.EntitySyncKey == "VERIFY-SKU-001" && e.Operation == "Create");
        results["Scenario4_ProductCreationOutbox"] = outboxCreate != null;

        // 3. Test Scenario 5: Product Commercial Update outbox event
        testProduct.Price = 1750.00m;
        testProduct.StockQuantity = 8;
        await _context.SaveChangesAsync();

        var outboxUpdate = await _context.SyncOutboxEvents
            .FirstOrDefaultAsync(e => e.EntityType == "Product" && e.EntitySyncKey == "VERIFY-SKU-001" && e.Operation == "Update");
        results["Scenario5_ProductCommercialUpdateOutbox"] = outboxUpdate != null;

        // 4. Test Scenario 6: Fragrance Only Update -> MUST NOT GENERATE OUTBOX EVENT
        int eventCountBefore = await _context.SyncOutboxEvents.CountAsync(e => e.EntitySyncKey == "VERIFY-SKU-001");
        testProduct.InspiredBy = "Maison Francis Kurkdjian";
        testProduct.Projection = Stockify.Models.Enums.FragranceProjection.Strong;
        await _context.SaveChangesAsync();
        int eventCountAfter = await _context.SyncOutboxEvents.CountAsync(e => e.EntitySyncKey == "VERIFY-SKU-001");
        results["Scenario6_FragranceOnlyUpdateSuppressed"] = (eventCountBefore == eventCountAfter);

        // 5. Test Scenario 7: Category Creation outbox event
        var testCat = new Category { Name = "VERIFY-CAT-001", Description = "Test Category" };
        _context.Categories.Add(testCat);
        await _context.SaveChangesAsync();

        var outboxCatCreate = await _context.SyncOutboxEvents
            .FirstOrDefaultAsync(e => e.EntityType == "Category" && e.EntitySyncKey == "VERIFY-CAT-001" && e.Operation == "Create");
        results["Scenario7_CategoryCreationOutbox"] = outboxCatCreate != null;

        // 6. Test Scenario 8: Category Update outbox event
        testCat.Description = "Updated Description";
        await _context.SaveChangesAsync();

        var outboxCatUpdate = await _context.SyncOutboxEvents
            .FirstOrDefaultAsync(e => e.EntityType == "Category" && e.EntitySyncKey == "VERIFY-CAT-001" && e.Operation == "Update");
        results["Scenario8_CategoryUpdateOutbox"] = outboxCatUpdate != null;

        // 7. Test Scenario 9: Outbox Event Suppression (SuppressSyncOutbox)
        int catEventsBefore = await _context.SyncOutboxEvents.CountAsync(e => e.EntitySyncKey == "VERIFY-CAT-001");
        _context.SuppressSyncOutbox = true;
        try
        {
            testCat.Description = "Suppressed Change";
            await _context.SaveChangesAsync();
        }
        finally
        {
            _context.SuppressSyncOutbox = false;
        }
        int catEventsAfter = await _context.SyncOutboxEvents.CountAsync(e => e.EntitySyncKey == "VERIFY-CAT-001");
        results["Scenario9_OutboxSuppressionActive"] = (catEventsBefore == catEventsAfter);

        // Cleanup test entities
        _context.Products.Remove(testProduct);
        _context.Categories.Remove(testCat);
        _context.SyncOutboxEvents.RemoveRange(await _context.SyncOutboxEvents.Where(e => e.EntitySyncKey == "VERIFY-SKU-001" || e.EntitySyncKey == "VERIFY-CAT-001").ToListAsync());
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = results.Values.All(v => v),
            results
        });
    }

    [HttpPost("push")]
    public async Task<IActionResult> PushEvents([FromBody] SyncPushRequestDto request)
    {
        if (!ValidateApiKey())
        {
            return Unauthorized(new { success = false, message = "Invalid or missing X-Sync-Api-Key header." });
        }

        if (request?.Events == null || request.Events.Count == 0)
        {
            return BadRequest(new { success = false, message = "No events provided in request." });
        }

        var results = new List<SyncItemResultDto>();

        // We process incoming remote changes with SuppressSyncOutbox = true to prevent infinite echo loops
        _context.SuppressSyncOutbox = true;
        SyncOutboxHelper.SuppressOutboxScope.Value = true;

        try
        {
            foreach (var evt in request.Events)
            {
                var itemResult = new SyncItemResultDto
                {
                    EventId = evt.EventId,
                    EntitySyncKey = evt.EntitySyncKey,
                    Success = false
                };

                try
                {
                    if (string.Equals(evt.EntityType, "Product", StringComparison.OrdinalIgnoreCase))
                    {
                        var productDto = JsonSerializer.Deserialize<ProductSyncDto>(evt.PayloadJson);
                        if (productDto == null || string.IsNullOrWhiteSpace(productDto.Sku))
                        {
                            itemResult.Error = "Invalid ProductSyncDto payload.";
                            results.Add(itemResult);
                            continue;
                        }

                        var existingProduct = await _context.Products
                            .Include(p => p.Category)
                            .FirstOrDefaultAsync(p => p.Sku == productDto.Sku);

                        if (string.Equals(evt.Operation, "Delete", StringComparison.OrdinalIgnoreCase))
                        {
                            if (existingProduct != null)
                            {
                                // Soft delete to protect existing order history and FK constraints
                                existingProduct.IsActive = false;
                                existingProduct.IsPublished = false;
                            }
                        }
                        else // Create or Update
                        {
                            // Ensure Category exists or map to correct category
                            Category? category = null;
                            if (!string.IsNullOrWhiteSpace(productDto.CategoryName))
                            {
                                category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == productDto.CategoryName);
                                if (category == null)
                                {
                                    category = new Category
                                    {
                                        Name = productDto.CategoryName,
                                        Description = $"Category created during synchronization for SKU {productDto.Sku}"
                                    };
                                    _context.Categories.Add(category);
                                    await _context.SaveChangesAsync();
                                }
                            }

                            if (existingProduct == null)
                            {
                                // New product: assign commercial properties. Find Your Scent fields remain default/unassigned.
                                var newProduct = new Product
                                {
                                    Sku = productDto.Sku,
                                    Name = productDto.Name,
                                    Description = productDto.Description,
                                    FragranceTagline = productDto.FragranceTagline,
                                    Price = productDto.Price,
                                    StockQuantity = productDto.StockQuantity,
                                    ReorderLevel = productDto.ReorderLevel,
                                    ImageUrl = productDto.ImageUrl,
                                    IsActive = productDto.IsActive,
                                    IsPublished = productDto.IsPublished,
                                    IsFeatured = productDto.IsFeatured,
                                    IsBestSeller = productDto.IsBestSeller,
                                    CategoryId = category?.Id ?? 2 // Fallback to Winter / default category
                                };
                                _context.Products.Add(newProduct);
                            }
                            else
                            {
                                // Existing product: UPDATE COMMERCIAL PROPERTIES ONLY!
                                // STRICT RULE: NEVER OVERWRITE FRAGRANCE PROFILE FIELDS
                                existingProduct.Name = productDto.Name;
                                existingProduct.Description = productDto.Description;
                                existingProduct.FragranceTagline = productDto.FragranceTagline;
                                existingProduct.Price = productDto.Price;
                                existingProduct.StockQuantity = productDto.StockQuantity;
                                existingProduct.ReorderLevel = productDto.ReorderLevel;
                                if (!string.IsNullOrWhiteSpace(productDto.ImageUrl))
                                {
                                    existingProduct.ImageUrl = productDto.ImageUrl;
                                }
                                existingProduct.IsActive = productDto.IsActive;
                                existingProduct.IsPublished = productDto.IsPublished;
                                existingProduct.IsFeatured = productDto.IsFeatured;
                                existingProduct.IsBestSeller = productDto.IsBestSeller;
                                if (category != null)
                                {
                                    existingProduct.CategoryId = category.Id;
                                }
                            }
                        }

                        // Append to Change Feed so other clients can pull it
                        var changeFeedEvent = new SyncChangeFeedEvent
                        {
                            EventId = evt.EventId != Guid.Empty ? evt.EventId : Guid.NewGuid(),
                            EntityType = "Product",
                            EntitySyncKey = productDto.Sku,
                            Operation = evt.Operation,
                            PayloadJson = evt.PayloadJson,
                            EntityVersion = evt.EntityVersion,
                            TimestampUtc = DateTime.UtcNow
                        };
                        _context.SyncChangeFeedEvents.Add(changeFeedEvent);

                        await _context.SaveChangesAsync();
                        itemResult.Success = true;
                    }
                    else if (string.Equals(evt.EntityType, "Category", StringComparison.OrdinalIgnoreCase))
                    {
                        var categoryDto = JsonSerializer.Deserialize<CategorySyncDto>(evt.PayloadJson);
                        if (categoryDto == null || string.IsNullOrWhiteSpace(categoryDto.Name))
                        {
                            itemResult.Error = "Invalid CategorySyncDto payload.";
                            results.Add(itemResult);
                            continue;
                        }

                        var existingCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == categoryDto.Name);

                        if (string.Equals(evt.Operation, "Delete", StringComparison.OrdinalIgnoreCase))
                        {
                            // If products exist in this category, we do not delete to protect FKs
                            if (existingCategory != null)
                            {
                                bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == existingCategory.Id);
                                if (!hasProducts)
                                {
                                    _context.Categories.Remove(existingCategory);
                                }
                            }
                        }
                        else
                        {
                            if (existingCategory == null)
                            {
                                var newCategory = new Category
                                {
                                    Name = categoryDto.Name,
                                    Description = categoryDto.Description,
                                    ImageUrl = categoryDto.ImageUrl
                                };
                                _context.Categories.Add(newCategory);
                            }
                            else
                            {
                                existingCategory.Description = categoryDto.Description;
                                if (!string.IsNullOrWhiteSpace(categoryDto.ImageUrl))
                                {
                                    existingCategory.ImageUrl = categoryDto.ImageUrl;
                                }
                            }
                        }

                        var changeFeedEvent = new SyncChangeFeedEvent
                        {
                            EventId = evt.EventId != Guid.Empty ? evt.EventId : Guid.NewGuid(),
                            EntityType = "Category",
                            EntitySyncKey = categoryDto.Name,
                            Operation = evt.Operation,
                            PayloadJson = evt.PayloadJson,
                            EntityVersion = evt.EntityVersion,
                            TimestampUtc = DateTime.UtcNow
                        };
                        _context.SyncChangeFeedEvents.Add(changeFeedEvent);

                        await _context.SaveChangesAsync();
                        itemResult.Success = true;
                    }
                    else
                    {
                        itemResult.Error = $"Unsupported EntityType: {evt.EntityType}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying sync event {EventId} for {EntitySyncKey}", evt.EventId, evt.EntitySyncKey);
                    itemResult.Error = ex.Message;
                }

                results.Add(itemResult);
            }

            var latestCursor = await _context.SyncChangeFeedEvents.OrderByDescending(c => c.Cursor).Select(c => c.Cursor).FirstOrDefaultAsync();

            return Ok(new SyncPushResponseDto
            {
                Success = results.All(r => r.Success),
                Message = $"Processed {results.Count} events. Succeeded: {results.Count(r => r.Success)}, Failed: {results.Count(r => !r.Success)}",
                Results = results,
                ServerCursor = latestCursor
            });
        }
        finally
        {
            _context.SuppressSyncOutbox = false;
            SyncOutboxHelper.SuppressOutboxScope.Value = false;
        }
    }

    [HttpGet("change-feed")]
    public async Task<IActionResult> GetChangeFeed([FromQuery] long cursor = 0, [FromQuery] int limit = 50)
    {
        if (!ValidateApiKey())
        {
            return Unauthorized(new { success = false, message = "Invalid or missing X-Sync-Api-Key header." });
        }

        if (limit <= 0 || limit > 100) limit = 50;

        var changes = await _context.SyncChangeFeedEvents
            .Where(e => e.Cursor > cursor)
            .OrderBy(e => e.Cursor)
            .Take(limit)
            .Select(e => new SyncItemEventDto
            {
                EventId = e.EventId,
                EntityType = e.EntityType,
                EntitySyncKey = e.EntitySyncKey,
                Operation = e.Operation,
                PayloadJson = e.PayloadJson,
                EntityVersion = e.EntityVersion,
                TimestampUtc = e.TimestampUtc
            })
            .ToListAsync();

        var maxCursor = await _context.SyncChangeFeedEvents
            .Where(e => e.Cursor > cursor)
            .OrderBy(e => e.Cursor)
            .Take(limit)
            .Select(e => e.Cursor)
            .LastOrDefaultAsync();

        var totalRemaining = await _context.SyncChangeFeedEvents
            .Where(e => e.Cursor > (maxCursor > 0 ? maxCursor : cursor))
            .CountAsync();

        return Ok(new ChangeFeedResponseDto
        {
            Success = true,
            CurrentCursor = maxCursor > 0 ? maxCursor : cursor,
            HasMore = totalRemaining > 0,
            Changes = changes
        });
    }

    [HttpPost("upload-media")]
    public async Task<IActionResult> UploadMedia([FromForm] IFormFile file, [FromForm] string? targetFolder = "products")
    {
        if (!ValidateApiKey())
        {
            return Unauthorized(new { success = false, message = "Invalid or missing X-Sync-Api-Key header." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new MediaUploadResponseDto { Success = false, Message = "No file provided." });
        }

        // Limit file size to 10MB
        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new MediaUploadResponseDto { Success = false, Message = "File size exceeds 10MB limit." });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(new MediaUploadResponseDto { Success = false, Message = $"Extension {ext} not permitted." });
        }

        // Validate Magic Bytes
        byte[] headerBytes = new byte[8];
        using (var reader = file.OpenReadStream())
        {
            await reader.ReadExactlyAsync(headerBytes, 0, 8);
        }

        bool validSignature = false;
        // JPEG: FF D8 FF
        if (headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF) validSignature = true;
        // PNG: 89 50 4E 47
        if (headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47) validSignature = true;
        // WebP / RIFF: 52 49 46 46
        if (headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46) validSignature = true;

        if (!validSignature)
        {
            return BadRequest(new MediaUploadResponseDto { Success = false, Message = "File content signature is invalid." });
        }

        string subFolder = string.Equals(targetFolder, "categories", StringComparison.OrdinalIgnoreCase) ? "categories" : "products";
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var targetDir = Path.Combine(webRoot, "roven", subFolder);
        Directory.CreateDirectory(targetDir);

        // Generate filename based on SHA-256 content hash to prevent duplicate files
        string sha256Hex;
        using (var sha256 = SHA256.Create())
        using (var stream = file.OpenReadStream())
        {
            var hashBytes = await sha256.ComputeHashAsync(stream);
            sha256Hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        var fileName = $"{sha256Hex}{ext}";
        var destinationPath = Path.Combine(targetDir, fileName);

        if (!System.IO.File.Exists(destinationPath))
        {
            using (var fileStream = new FileStream(destinationPath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
        }

        var relativeUrl = $"/roven/{subFolder}/{fileName}";
        return Ok(new MediaUploadResponseDto
        {
            Success = true,
            RelativeUrl = relativeUrl,
            Sha256Hash = sha256Hex,
            Message = "File uploaded successfully."
        });
    }
}
