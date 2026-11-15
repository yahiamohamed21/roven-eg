using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Stockify.Common.Security;

/// <summary>
/// Security validator for uploaded images. Performs MIME-type validation,
/// extension matching, magic byte signature checks, file size verification,
/// and SVG sanitization against XSS/payload injection.
/// </summary>
public static class ImageSecurityValidator
{
    // Strictly allowed raster formats for all user uploads (Products & Banners)
    // SVG uploads are explicitly disabled to eliminate XSS / XML Injection vectors.
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly string[] AllowedMimeTypes =
    [
        "image/jpeg",
        "image/pjpeg",
        "image/png",
        "image/x-png",
        "image/webp"
    ];

    // Magic Bytes signatures
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffHeader = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpHeader = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    public static bool Validate(
        IFormFile? file,
        out string? errorMessage,
        bool allowSvg = false,
        long maxSizeBytes = 5 * 1024 * 1024)
    {
        errorMessage = null;

        if (file == null || file.Length == 0)
        {
            errorMessage = "No file was uploaded or the file is empty.";
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mime = file.ContentType.ToLowerInvariant();

        // Explicit security rule: Reject all SVG uploads regardless of parameter
        if (ext == ".svg" || mime.Contains("svg"))
        {
            errorMessage = "SVG image uploads are strictly disabled for security reasons. Please use JPG, PNG, or WebP formats.";
            return false;
        }

        // 1. File size check
        if (file.Length > maxSizeBytes)
        {
            var maxMb = Math.Round((double)maxSizeBytes / (1024 * 1024), 1);
            errorMessage = $"Image file size cannot exceed {maxMb}MB.";
            return false;
        }

        // 2. Extension check
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            errorMessage = "Permitted image formats: JPG, PNG, WebP.";
            return false;
        }

        // 3. MIME type check
        if (!AllowedMimeTypes.Contains(mime))
        {
            errorMessage = $"Invalid file MIME type: {file.ContentType}. Permitted types: image/jpeg, image/png, image/webp.";
            return false;
        }

        // 4. Magic bytes / file signature verification
        try
        {
            using var stream = file.OpenReadStream();

            // Binary images signature validation
            var headerBytes = new byte[16];
            var bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);
            if (bytesRead < 4)
            {
                errorMessage = "File header is corrupted or incomplete.";
                return false;
            }

            if (ext is ".jpg" or ".jpeg")
            {
                if (!headerBytes.Take(3).SequenceEqual(JpegHeader))
                {
                    errorMessage = "File content does not match standard JPEG signature.";
                    return false;
                }
            }
            else if (ext == ".png")
            {
                if (bytesRead < 8 || !headerBytes.Take(8).SequenceEqual(PngHeader))
                {
                    errorMessage = "File content does not match standard PNG signature.";
                    return false;
                }
            }
            else if (ext == ".webp")
            {
                // WebP starts with "RIFF" at offset 0, and has "WEBP" at offset 8
                if (bytesRead < 12 ||
                    !headerBytes.Take(4).SequenceEqual(RiffHeader) ||
                    !headerBytes.Skip(8).Take(4).SequenceEqual(WebpHeader))
                {
                    errorMessage = "File content does not match standard WebP signature.";
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Unable to read file stream: {ex.Message}";
            return false;
        }

        return true;
    }

    private static bool ValidateSvgContent(Stream stream, out string? error)
    {
        error = null;
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var content = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(content))
            {
                error = "SVG file is empty.";
                return false;
            }

            var lower = content.ToLowerInvariant();

            // Must contain an <svg tag
            if (!lower.Contains("<svg") || !lower.Contains("</svg>"))
            {
                error = "File does not contain valid SVG markup.";
                return false;
            }

            // Check for unsafe script or event handler elements
            string[] forbiddenPatterns =
            [
                "<script",
                "</script>",
                "javascript:",
                "data:text/html",
                "onload=",
                "onerror=",
                "onclick=",
                "onmouseover=",
                "<iframe",
                "<embed",
                "<object"
            ];

            foreach (var forbidden in forbiddenPatterns)
            {
                if (lower.Contains(forbidden))
                {
                    error = $"SVG file contains potentially unsafe content ({forbidden}).";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to parse SVG: {ex.Message}";
            return false;
        }
    }
}
