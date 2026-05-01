using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Core.Interfaces;

public interface ITextExtractionService
{
    Task<TextExtractionResult> ExtractTextAsync(
        string physicalPath,
        string originalFileName,
        CancellationToken cancellationToken = default);
}
