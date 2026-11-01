using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Core.Services;

public class FormatDetector : IFormatDetector
{
    private static readonly Dictionary<string, (FileCategory Category, string MimeType)> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".png", (FileCategory.Image, "image/png") },
        { ".jpg", (FileCategory.Image, "image/jpeg") },
        { ".jpeg", (FileCategory.Image, "image/jpeg") },
        { ".webp", (FileCategory.Image, "image/webp") },
        { ".avif", (FileCategory.Image, "image/avif") },
        { ".svg", (FileCategory.Image, "image/svg+xml") },
        { ".tiff", (FileCategory.Image, "image/tiff") },
        { ".tif", (FileCategory.Image, "image/tiff") },
        { ".bmp", (FileCategory.Image, "image/bmp") },
        { ".gif", (FileCategory.Image, "image/gif") },

        // Audio
        { ".mp3", (FileCategory.Audio, "audio/mpeg") },
        { ".wav", (FileCategory.Audio, "audio/wav") },
        { ".flac", (FileCategory.Audio, "audio/flac") },
        { ".aac", (FileCategory.Audio, "audio/aac") },
        { ".ogg", (FileCategory.Audio, "audio/ogg") },
        { ".m4a", (FileCategory.Audio, "audio/mp4") },
        { ".wma", (FileCategory.Audio, "audio/x-ms-wma") },

        // Video
        { ".mp4", (FileCategory.Video, "video/mp4") },
        { ".mkv", (FileCategory.Video, "video/x-matroska") },
        { ".mov", (FileCategory.Video, "video/quicktime") },
        { ".avi", (FileCategory.Video, "video/x-msvideo") },
        { ".webm", (FileCategory.Video, "video/webm") },
        { ".wmv", (FileCategory.Video, "video/x-ms-wmv") },
        { ".flv", (FileCategory.Video, "video/x-flv") },

        // Documents
        { ".pdf", (FileCategory.Document, "application/pdf") },
        { ".docx", (FileCategory.Document, "application/vnd.openxmlformats-officedocument.wordprocessingml.document") },
        { ".doc", (FileCategory.Document, "application/msword") },
        { ".txt", (FileCategory.Document, "text/plain") },
        { ".odt", (FileCategory.Document, "application/vnd.oasis.opendocument.text") },
        { ".rtf", (FileCategory.Document, "application/rtf") },

        // Archives
        { ".zip", (FileCategory.Archive, "application/zip") },
        { ".7z", (FileCategory.Archive, "application/x-7z-compressed") },
        { ".rar", (FileCategory.Archive, "application/vnd.rar") },
        { ".tar", (FileCategory.Archive, "application/x-tar") },
        { ".gz", (FileCategory.Archive, "application/gzip") }
    };

    private static readonly Dictionary<FileCategory, string[]> TargetFormatsMap = new()
    {
        { FileCategory.Image, new[] { "png", "jpg", "webp", "avif", "bmp", "tiff", "gif" } },
        { FileCategory.Audio, new[] { "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma" } },
        { FileCategory.Video, new[] { "mp4", "mkv", "mov", "avi", "webm" } },
        { FileCategory.Document, new[] { "pdf", "docx", "txt", "rtf" } },
        { FileCategory.Archive, new[] { "zip", "tar.gz", "7z" } }
    };

    public FormatDetectionResult DetectFormat(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new FormatDetectionResult { Category = FileCategory.Unknown, IsSupportedInput = false };
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ExtensionMap.TryGetValue(ext, out var info))
        {
            return new FormatDetectionResult
            {
                Extension = ext.TrimStart('.'),
                MimeType = info.MimeType,
                Category = info.Category,
                IsSupportedInput = true,
                RecommendedOutputFormats = GetSupportedTargetFormats(info.Category)
            };
        }

        return new FormatDetectionResult
        {
            Extension = ext.TrimStart('.'),
            MimeType = "application/octet-stream",
            Category = FileCategory.Unknown,
            IsSupportedInput = false,
            RecommendedOutputFormats = Array.Empty<string>()
        };
    }

    public bool IsSupportedSource(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && ExtensionMap.ContainsKey(ext);
    }

    public IReadOnlyList<string> GetSupportedTargetFormats(FileCategory category)
    {
        if (TargetFormatsMap.TryGetValue(category, out var formats))
        {
            return formats;
        }

        return Array.Empty<string>();
    }
}
