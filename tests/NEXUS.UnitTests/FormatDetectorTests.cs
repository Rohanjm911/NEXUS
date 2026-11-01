using NEXUS.Core.Enums;
using NEXUS.Core.Services;
using Xunit;

namespace NEXUS.UnitTests;

public class FormatDetectorTests
{
    private readonly FormatDetector _detector = new();

    [Theory]
    [InlineData("photo.png", FileCategory.Image, "image/png")]
    [InlineData("image.jpg", FileCategory.Image, "image/jpeg")]
    [InlineData("graphic.webp", FileCategory.Image, "image/webp")]
    [InlineData("sample.avif", FileCategory.Image, "image/avif")]
    [InlineData("song.mp3", FileCategory.Audio, "audio/mpeg")]
    [InlineData("track.flac", FileCategory.Audio, "audio/flac")]
    [InlineData("movie.mp4", FileCategory.Video, "video/mp4")]
    [InlineData("clip.mkv", FileCategory.Video, "video/x-matroska")]
    [InlineData("report.pdf", FileCategory.Document, "application/pdf")]
    [InlineData("notes.txt", FileCategory.Document, "text/plain")]
    [InlineData("archive.zip", FileCategory.Archive, "application/zip")]
    [InlineData("backup.7z", FileCategory.Archive, "application/x-7z-compressed")]
    public void DetectFormat_ValidExtension_ReturnsCorrectCategoryAndMime(string filename, FileCategory expectedCategory, string expectedMime)
    {
        var result = _detector.DetectFormat(filename);

        Assert.True(result.IsSupportedInput);
        Assert.Equal(expectedCategory, result.Category);
        Assert.Equal(expectedMime, result.MimeType);
        Assert.NotEmpty(result.RecommendedOutputFormats);
    }

    [Fact]
    public void DetectFormat_UnknownExtension_ReturnsUnknownCategory()
    {
        var result = _detector.DetectFormat("file.xyz123");

        Assert.False(result.IsSupportedInput);
        Assert.Equal(FileCategory.Unknown, result.Category);
        Assert.Empty(result.RecommendedOutputFormats);
    }

    [Fact]
    public void IsSupportedSource_ReturnsExpectedBoolean()
    {
        Assert.True(_detector.IsSupportedSource("test.png"));
        Assert.True(_detector.IsSupportedSource("audio.wav"));
        Assert.False(_detector.IsSupportedSource("unknown.random"));
        Assert.False(_detector.IsSupportedSource(""));
    }
}

