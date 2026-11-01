using NEXUS.Core.Enums;
using NEXUS.Core.Interfaces;
using NEXUS.Core.Models;

namespace NEXUS.Infrastructure.Services;

public class StorageAnalyzerService : IStorageAnalyzerService
{
    private readonly IFormatDetector _formatDetector;

    public StorageAnalyzerService(IFormatDetector formatDetector)
    {
        _formatDetector = formatDetector;
    }

    public Task<IReadOnlyList<StorageCategoryStats>> AnalyzeDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Task.FromResult<IReadOnlyList<StorageCategoryStats>>(Array.Empty<StorageCategoryStats>());
        }

        return Task.Run(() =>
        {
            var categoryMap = new Dictionary<FileCategory, (int Count, long Bytes)>
            {
                { FileCategory.Image, (0, 0L) },
                { FileCategory.Audio, (0, 0L) },
                { FileCategory.Video, (0, 0L) },
                { FileCategory.Document, (0, 0L) },
                { FileCategory.Archive, (0, 0L) },
                { FileCategory.Code, (0, 0L) },
                { FileCategory.Unknown, (0, 0L) }
            };

            long totalBytes = 0;

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                foreach (var file in Directory.EnumerateFiles(directoryPath, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var info = new FileInfo(file);
                        var length = info.Length;
                        totalBytes += length;

                        var detection = _formatDetector.DetectFormat(file);
                        var cat = detection.Category;

                        var current = categoryMap[cat];
                        categoryMap[cat] = (current.Count + 1, current.Bytes + length);
                    }
                    catch
                    {
                        // Skip unreadable files
                    }
                }
            }
            catch
            {
                // Return whatever was collected so far
            }

            var results = new List<StorageCategoryStats>();
            foreach (var kvp in categoryMap)
            {
                if (kvp.Value.Count > 0)
                {
                    results.Add(new StorageCategoryStats
                    {
                        Category = kvp.Key,
                        TotalFiles = kvp.Value.Count,
                        TotalSizeBytes = kvp.Value.Bytes,
                        Percentage = totalBytes > 0 ? Math.Round((kvp.Value.Bytes * 100.0) / totalBytes, 2) : 0.0
                    });
                }
            }

            return (IReadOnlyList<StorageCategoryStats>)results.OrderByDescending(s => s.TotalSizeBytes).ToList();
        }, cancellationToken);
    }
}
