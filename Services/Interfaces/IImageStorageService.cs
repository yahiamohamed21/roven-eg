using Microsoft.AspNetCore.Http;

namespace Stockify.Services.Interfaces;

public interface IImageStorageService
{
    bool IsConfigured { get; }
    Task<string> UploadImageAsync(IFormFile file, string folder, string? customFileName = null);
    Task<string> UploadFileAsync(Stream stream, string fileName, string folder);
    Task<bool> DeleteImageAsync(string imageUrl);
}

