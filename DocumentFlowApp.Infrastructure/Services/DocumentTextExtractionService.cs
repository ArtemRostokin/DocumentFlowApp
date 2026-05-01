using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DocumentFlowApp.Infrastructure.Services;

public sealed class DocumentTextExtractionService : ITextExtractionService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx"
    };

    private readonly ILogger<DocumentTextExtractionService> _logger;

    public DocumentTextExtractionService(ILogger<DocumentTextExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        string physicalPath,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!SupportedExtensions.Contains(extension ?? string.Empty))
        {
            return new TextExtractionResult
            {
                IsSuccessful = false,
                Provider = "unsupported",
                Summary = "Извлечение текста доступно только для PDF и DOCX."
            };
        }

        if (!File.Exists(physicalPath))
        {
            return new TextExtractionResult
            {
                IsSuccessful = false,
                Provider = "missing-file",
                Summary = "Файл для извлечения текста не найден."
            };
        }

        try
        {
            var extractedText = extension?.ToLowerInvariant() switch
            {
                ".docx" => await ExtractDocxTextAsync(physicalPath, cancellationToken),
                ".pdf" => ExtractPdfText(physicalPath),
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                return new TextExtractionResult
                {
                    IsSuccessful = true,
                    Provider = extension?.TrimStart('.').ToLowerInvariant() switch
                    {
                        "docx" => "docx-xml",
                        "pdf" => "pdf-text",
                        _ => "text-extractor"
                    },
                    ExtractedText = extractedText,
                    ConfidenceScore = EstimateConfidence(extractedText),
                    Summary = extension?.Equals(".docx", StringComparison.OrdinalIgnoreCase) == true
                        ? "Текст извлечён из DOCX-документа."
                        : "Текст извлечён из PDF-документа."
                };
            }

            return BuildFallbackResult(originalFileName, $"{extension?.TrimStart('.').ToLowerInvariant()}-filename-fallback");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Text extraction failed for {FileName}", originalFileName);
            return BuildFallbackResult(originalFileName, $"{extension?.TrimStart('.').ToLowerInvariant()}-filename-fallback");
        }
    }

    private static async Task<string> ExtractDocxTextAsync(string physicalPath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(physicalPath);
        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
            return string.Empty;

        using var stream = documentEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var xml = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
            return string.Empty;

        var xdoc = XDocument.Parse(xml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var paragraphs = xdoc
            .Descendants(w + "p")
            .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return NormalizeWhitespace(string.Join(Environment.NewLine, paragraphs));
    }

    private static string ExtractPdfText(string physicalPath)
    {
        using var document = PdfDocument.Open(physicalPath);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (string.IsNullOrWhiteSpace(pageText))
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.AppendLine(pageText);
        }

        return NormalizeWhitespace(builder.ToString());
    }

    private static TextExtractionResult BuildFallbackResult(string originalFileName, string provider)
    {
        var extractedText = NormalizeFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return new TextExtractionResult
            {
                IsSuccessful = false,
                IsFallback = true,
                Provider = provider,
                Summary = "Не удалось извлечь текст из файла."
            };
        }

        return new TextExtractionResult
        {
            IsSuccessful = true,
            IsFallback = true,
            Provider = provider,
            ExtractedText = extractedText,
            ConfidenceScore = 0.49m,
            Summary = "Текстовые признаки взяты из имени файла."
        };
    }

    private static string NormalizeFileName(string originalFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(baseName))
            return string.Empty;

        return NormalizeWhitespace(baseName.Replace('_', ' ').Replace('-', ' ').Replace('.', ' '));
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static decimal EstimateConfidence(string extractedText)
    {
        var length = extractedText.Length;
        if (length >= 300)
            return 0.89m;
        if (length >= 120)
            return 0.82m;
        if (length >= 50)
            return 0.74m;
        return 0.61m;
    }
}
