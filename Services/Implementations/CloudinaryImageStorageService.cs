using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        if (!string.IsNullOrWhiteSpace(cloudName) &&
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(apiSecret) &&
            !cloudName.Contains("your-cloud-name", StringComparison.OrdinalIgnoreCase))
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
            _logger.LogWarning("Cloudinary credentials not configured. Falling back to local WebRoot storage.");
        }
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, string? customFileName = null)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Uploaded file cannot be null or empty.", nameof(file));
        }

        if (_isConfigured && _cloudinary != null)
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
            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary upload failed: {Error}", uploadResult.Error.Message);
                throw new InvalidOperationException($"Cloudinary image upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty;
        }

        // Fallback to local storage if Cloudinary is not yet configured with API keys
        return await SaveLocalFileAsync(file, folder, customFileName);
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string folder)
    {
        if (_isConfigured && _cloudinary != null)
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
            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task<bool> DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return false;

        if (_isConfigured && _cloudinary != null && imageUrl.Contains("cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(imageUrl);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var uploadIndex = Array.IndexOf(segments, "upload");
                if (uploadIndex >= 0 && uploadIndex < segments.Length - 1)
                {
                    var pathSegments = segments.Skip(uploadIndex + 1).Where(s => !s.StartsWith("v") || !long.TryParse(s[1..], out _)).ToArray();
                    var publicIdWithExt = string.Join("/", pathSegments);
                    var publicId = Path.ChangeExtension(publicIdWithExt, null);

                    var deletionParams = new DeletionParams(publicId);
                    var result = await _cloudinary.DestroyAsync(deletionParams);
                    return result.Result == "ok";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse and delete Cloudinary image: {Url}", imageUrl);
            }
        }

        return false;
    }

    private async Task<string> SaveLocalFileAsync(IFormFile file, string folder, string? customFileName)
    {
        var targetFolder = Path.Combine(_environment.WebRootPath, folder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName
            : $"{Guid.NewGuid():N}{ext}";

        var filePath = Path.Combine(targetFolder, uniqueName);
        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return $"/{folder.Trim('/')}/{uniqueName}";
    }
}

