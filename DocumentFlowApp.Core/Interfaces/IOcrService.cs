using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Core.Interfaces;

public interface IOcrService
{
    Task<OcrExtractionResult> ExtractTextAsync(
        string physicalPath,
        string originalFileName,
        CancellationToken cancellationToken = default);
}
