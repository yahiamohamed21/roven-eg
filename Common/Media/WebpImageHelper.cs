using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Stockify.Common.Media;

/// <summary>
/// Shared helper for decoding, orienting, optionally resizing, and encoding raster images into lightweight WebP format.
/// </summary>
public static class WebpImageHelper
{
    private static readonly HashSet<string> ConvertibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif"
    };

    public static bool IsConvertibleRasterExtension(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return false;
        var clean = pathOrUrl.Split('?', '#')[0];
        var ext = Path.GetExtension(clean);
        return ConvertibleExtensions.Contains(ext);
    }

    public static string ChangeUrlExtensionToWebp(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        var qIndex = url.IndexOfAny(new[] { '?', '#' });
        var basePart = qIndex >= 0 ? url[..qIndex] : url;
        var suffix = qIndex >= 0 ? url[qIndex..] : string.Empty;
        var ext = Path.GetExtension(basePart);
        if (string.IsNullOrEmpty(ext) || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) || ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }
        return $"{basePart[..^ext.Length]}.webp{suffix}";
    }

    public static async Task SaveStreamAsWebpAsync(
        Stream sourceStream,
        string targetWebpPath,
        int maxDimension = 1400,
        int quality = 82)
    {
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
        }

        using var image = await Image.LoadAsync(sourceStream);
        image.Mutate(x =>
        {
            x.AutoOrient();
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                x.Resize(new ResizeOptions
                {
                    Size = new Size(maxDimension, maxDimension),
                    Mode = ResizeMode.Max
                });
            }
        });

        var dir = Path.GetDirectoryName(targetWebpPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var encoder = new WebpEncoder
        {
            Quality = quality,
            FileFormat = WebpFileFormatType.Lossy
        };

        var tempPath = $"{targetWebpPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var outStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await image.SaveAsWebpAsync(outStream, encoder);
            }

            if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
            {
                File.Move(tempPath, targetWebpPath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    public static async Task<bool> ConvertLocalFileToWebpAsync(
        string sourcePath,
        string targetWebpPath,
        bool deleteSourceAfterSuccess = true,
        int maxDimension = 1400,
        int quality = 82)
    {
        if (!File.Exists(sourcePath)) return false;

        var info = new FileInfo(sourcePath);
        if (info.Length < 100) return false;

        try
        {
            await using (var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await SaveStreamAsWebpAsync(stream, targetWebpPath, maxDimension, quality);
            }

            if (File.Exists(targetWebpPath) && new FileInfo(targetWebpPath).Length > 0)
            {
                if (deleteSourceAfterSuccess && !string.Equals(sourcePath, targetWebpPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(sourcePath); } catch { }
                }
                return true;
            }
        }
        catch
        {
            // Ignore malformed or unsupported local image files so app startup continues cleanly
        }

        return false;
    }
}
