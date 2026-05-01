using DocumentFlowApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentFlowApp.Tests;

public sealed class WindowsOcrServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_Falls_Back_To_FileName_When_Ocr_Cannot_Read_Image()
    {
        var service = new WindowsOcrService(NullLogger<WindowsOcrService>.Instance);
        var tempFile = Path.Combine(Path.GetTempPath(), $"schet_april_{Guid.NewGuid():N}.jpg");

        await File.WriteAllBytesAsync(tempFile, [1, 2, 3, 4]);

        try
        {
            var result = await service.ExtractTextAsync(tempFile, "schet_april.jpg");

            Assert.True(result.IsSuccessful);
            Assert.True(result.IsFallback);
            Assert.Equal("schet april", result.ExtractedText);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
