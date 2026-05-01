using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Tests.TestDoubles;

public sealed class FakeOcrService : IOcrService
{
    public OcrExtractionResult NextResult { get; set; } = new()
    {
        IsSuccessful = false,
        Provider = "fake-ocr",
        Summary = "OCR не запускался в тесте."
    };

    public Task<OcrExtractionResult> ExtractTextAsync(string physicalPath, string originalFileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NextResult);
    }
}
