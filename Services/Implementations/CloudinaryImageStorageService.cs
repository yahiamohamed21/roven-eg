using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stockify.Common.Media;
using Stockify.Services.Interfaces;

namespace Stockify.Services.Implementations;

public class CloudinaryImageStorageService : IImageStorageService
{
    private readonly Cloudinary? _cloudinary;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CloudinaryImageStorageService> _logger;
    private readonly bool _isConfigured;

    public bool IsConfigured => _isConfigured;

    public CloudinaryImageStorageService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<CloudinaryImageStorageService> logger)
    {
        _environment = environment;
        _logger = logger;

        var cloudName = configuration["Cloudinary:CloudName"] 
                        ?? Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
        var apiKey = configuration["Cloudinary:ApiKey"] 
                     ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
        var apiSecret = configuration["Cloudinary:ApiSecret"] 
                       ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

        bool isPlaceholder = string.IsNullOrWhiteSpace(cloudName) ||
                             string.IsNullOrWhiteSpace(apiKey) ||
                             string.IsNullOrWhiteSpace(apiSecret) ||
                             cloudName.Contains("your", StringComparison.OrdinalIgnoreCase) ||
                             apiKey.Contains("your", StringComparison.OrdinalIgnoreCase) ||
                             apiSecret.Contains("your", StringComparison.OrdinalIgnoreCase) ||
                             cloudName.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

        if (!isPlaceholder)
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
            _isConfigured = true;
            _logger.LogInformation("Cloudinary Image Storage initialized successfully for cloud: {CloudName}", cloudName);
        }
        else
        {
            _isConfigured = false;
            _logger.LogWarning("Cloudinary credentials not configured or using placeholders. Falling back to local WebRoot storage.");
        }
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, string? customFileName = null)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Uploaded file cannot be null or empty.", nameof(file));
        }

        bool allowSvg = folder.Contains("banners", StringComparison.OrdinalIgnoreCase);
        if (!Stockify.Common.Security.ImageSecurityValidator.Validate(file, out var validationError, allowSvg))
        {
            _logger.LogWarning("Rejected invalid image upload ({FileName}): {Error}", file.FileName, validationError);
            throw new ArgumentException(validationError ?? "Invalid image file.", nameof(file));
        }

        // 1. Try Cloudinary if configured with valid credentials
        if (_isConfigured && _cloudinary != null)
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var publicId = !string.IsNullOrWhiteSpace(customFileName)
                    ? Path.GetFileNameWithoutExtension(customFileName)
                    : $"{Guid.NewGuid():N}";

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    PublicId = publicId,
                    Format = "webp",
                    Transformation = new Transformation()
                        .FetchFormat("webp")
                        .Quality("auto:good"),
                    Overwrite = true
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error == null)
                {
                    var secureUrl = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
                    if (!string.IsNullOrWhiteSpace(secureUrl))
                    {
                        return secureUrl;
                    }
                }

                _logger.LogWarning("Cloudinary upload failed ({Error}), falling back to local WebRoot storage.", uploadResult.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudinary upload exception encountered. Falling back gracefully to local WebRoot storage.");
            }
        }

        // 2. Guaranteed local storage fallback
        return await SaveLocalFileAsync(file, folder, customFileName);
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string folder)
    {
        if (_isConfigured && _cloudinary != null)
        {
            try
            {
                var publicId = Path.GetFileNameWithoutExtension(fileName);
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, stream),
                    Folder = folder,
                    PublicId = publicId,
                    Format = "webp",
                    Transformation = new Transformation()
                        .FetchFormat("webp")
                        .Quality("auto:good"),
                    Overwrite = true
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error == null)
                {
                    var secureUrl = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
                    if (!string.IsNullOrWhiteSpace(secureUrl))
                    {
                        return secureUrl;
                    }
                }

                _logger.LogWarning("Cloudinary stream upload failed ({Error}), falling back to local storage.", uploadResult.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudinary stream upload exception. Falling back to local storage.");
            }
        }

        return await SaveLocalStreamAsync(stream, folder, fileName);
    }

    public async Task<bool> DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return false;

        // 1. Cloudinary deletion
        if (_isConfigured && _cloudinary != null && imageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(imageUrl);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var uploadIndex = Array.IndexOf(segments, "upload");
                if (uploadIndex >= 0 && uploadIndex < segments.Length - 1)
                {
                    var pathSegments = segments.Skip(uploadIndex + 1)
                        .Where(s => (!s.StartsWith("v") || !long.TryParse(s[1..], out _)) && !s.Contains(','))
                        .ToArray();
                    var publicIdWithExt = string.Join("/", pathSegments);
                    var publicId = Path.ChangeExtension(publicIdWithExt, null);

                    var deletionParams = new DeletionParams(publicId);
                    var result = await _cloudinary.DestroyAsync(deletionParams);
                    _logger.LogInformation("Cloudinary asset deletion result ({PublicId}): {Result}", publicId, result.Result);
                    return result.Result == "ok";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse and delete Cloudinary image: {Url}", imageUrl);
            }
        }

        // 2. Local file deletion fallback
        if (!imageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase) && imageUrl.StartsWith("/"))
        {
            try
            {
                var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
                    ? _environment.WebRootPath
                    : Path.Combine(_environment.ContentRootPath, "wwwroot");
                var localPath = Path.Combine(webRoot, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete local image file: {Url}", imageUrl);
            }
        }

        return false;
    }

    private async Task<string> SaveLocalFileAsync(IFormFile file, string folder, string? customFileName)
    {
        await using var stream = file.OpenReadStream();
        return await SaveLocalStreamInternalAsync(stream, folder, file.FileName, customFileName);
    }

    private Task<string> SaveLocalStreamAsync(Stream stream, string folder, string fileName)
    {
        return SaveLocalStreamInternalAsync(stream, folder, fileName, null);
    }

    private async Task<string> SaveLocalStreamInternalAsync(Stream stream, string folder, string originalFileName, string? customFileName)
    {
        var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? _environment.WebRootPath
            : Path.Combine(_environment.ContentRootPath, "wwwroot");

        var targetFolder = Path.Combine(webRoot, folder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        var origExt = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (origExt == ".svg")
        {
            var svgName = !string.IsNullOrWhiteSpace(customFileName)
                ? Path.ChangeExtension(customFileName, ".svg")
                : $"{Guid.NewGuid():N}.svg";
            var svgPath = Path.Combine(targetFolder, svgName);
            await using var svgStream = new FileStream(svgPath, FileMode.Create);
            if (stream.CanSeek) stream.Position = 0;
            await stream.CopyToAsync(svgStream);
            return $"/{folder.Trim('/')}/{svgName}";
        }

        // Force .webp conversion for all uploaded raster images to minimize server bandwidth and storage load
        var webpName = !string.IsNullOrWhiteSpace(customFileName)
            ? Path.ChangeExtension(customFileName, ".webp")
            : $"{Guid.NewGuid():N}.webp";
        var webpPath = Path.Combine(targetFolder, webpName);

        try
        {
            await WebpImageHelper.SaveStreamAsWebpAsync(stream, webpPath);
            if (File.Exists(webpPath) && new FileInfo(webpPath).Length > 0)
            {
                return $"/{folder.Trim('/')}/{webpName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebP conversion failed for {FileName}, falling back to raw file save.", originalFileName);
        }

        var fallbackExt = string.IsNullOrEmpty(origExt) ? ".webp" : origExt;
        var fallbackName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName
            : $"{Guid.NewGuid():N}{fallbackExt}";
        var fallbackPath = Path.Combine(targetFolder, fallbackName);
        await using (var fileStream = new FileStream(fallbackPath, FileMode.Create))
        {
            if (stream.CanSeek) stream.Position = 0;
            await stream.CopyToAsync(fileStream);
        }

        return $"/{folder.Trim('/')}/{fallbackName}";
    }
}

