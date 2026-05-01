using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Tests.TestDoubles;

public sealed class FakeTextExtractionService : ITextExtractionService
{
    public Task<TextExtractionResult> ExtractTextAsync(
        string physicalPath,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var extractedText = Path.GetFileNameWithoutExtension(originalFileName)
            .Replace('_', ' ')
            .Replace('-', ' ');

        return Task.FromResult(new TextExtractionResult
        {
            IsSuccessful = !string.IsNullOrWhiteSpace(extractedText),
            Provider = "fake-text-extractor",
            ExtractedText = extractedText,
            ConfidenceScore = 0.64m,
            Summary = "Текст извлечен тестовым сервисом."
        });
    }
}
