using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace DocumentFlowApp.Tests;

public sealed class ApplicationDatasetEvaluatorTests
{
    private static readonly string[] SupportedExtensions = [".pdf", ".docx", ".png", ".jpg", ".jpeg"];
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];

    private readonly ITestOutputHelper _output;

    public ApplicationDatasetEvaluatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Evaluate_Application_Dataset_And_Write_Report()
    {
        var datasetRoot = ResolveDatasetRoot();
        if (string.IsNullOrWhiteSpace(datasetRoot) || !Directory.Exists(datasetRoot))
        {
            _output.WriteLine("Dataset root was not found. Set DOCUMENTFLOWAPP_DATASET_ROOT or place the dataset next to the repo.");
            return;
        }

        var trainPath = Path.Combine(datasetRoot, "train");
        var testPath = Path.Combine(datasetRoot, "test");
        Assert.True(Directory.Exists(trainPath), $"Train folder was not found: {trainPath}");
        Assert.True(Directory.Exists(testPath), $"Test folder was not found: {testPath}");

        var textService = new DocumentTextExtractionService(NullLogger<DocumentTextExtractionService>.Instance);
        var ocrService = new WindowsOcrService(NullLogger<WindowsOcrService>.Instance);
        var fieldExtractor = new RuleBasedDocumentFieldExtractor();

        var report = new ApplicationDatasetEvaluationReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DatasetRoot = datasetRoot,
            Splits =
            [
                await EvaluateSplitAsync("train", trainPath, textService, ocrService, fieldExtractor),
                await EvaluateSplitAsync("test", testPath, textService, ocrService, fieldExtractor)
            ]
        };

        var repoRoot = ResolveRepoRoot();
        var outputDir = Path.Combine(repoRoot, "artifacts", "dataset-evaluation");
        Directory.CreateDirectory(outputDir);

        var jsonPath = Path.Combine(outputDir, "application-dataset-evaluation.json");
        var markdownPath = Path.Combine(outputDir, "application-dataset-evaluation.md");

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions), Encoding.UTF8);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), Encoding.UTF8);

        foreach (var split in report.Splits)
        {
            _output.WriteLine(
                $"{split.Split}: files={split.TotalFiles}, success={split.SuccessfulExtractions}, fallback={split.FallbackExtractions}, matched={split.TotalMatchedFields}/{split.TotalExpectedFields}, recall={split.FieldRecall:P1}");

            foreach (var file in split.Files.Where(file => file.MissingFields.Count > 0 || file.MismatchedFields.Count > 0))
            {
                _output.WriteLine(
                    $"  {file.FileName}: missing={file.MissingFields.Count}, mismatched={file.MismatchedFields.Count}, provider={file.Provider}");
            }
        }

        _output.WriteLine($"JSON report: {jsonPath}");
        _output.WriteLine($"Markdown report: {markdownPath}");

        Assert.True(report.Splits.Sum(split => split.TotalFiles) > 0, "Dataset is empty.");
    }

    private static async Task<DatasetSplitReport> EvaluateSplitAsync(
        string splitName,
        string splitPath,
        DocumentTextExtractionService textService,
        WindowsOcrService ocrService,
        RuleBasedDocumentFieldExtractor fieldExtractor)
    {
        var report = new DatasetSplitReport
        {
            Split = splitName,
            FolderPath = splitPath
        };

        var files = Directory
            .EnumerateFiles(splitPath)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var filePath in files)
        {
            var annotationPath = Path.ChangeExtension(filePath, ".json");
            Assert.True(File.Exists(annotationPath), $"Annotation file was not found for {filePath}");

            await using var stream = File.OpenRead(annotationPath);
            var annotation = await JsonSerializer.DeserializeAsync<ApplicationAnnotation>(stream)
                             ?? new ApplicationAnnotation();

            var textSnapshot = await ExtractTextAsync(filePath, textService, ocrService);
            var extractedFields = fieldExtractor
                .Extract(DocumentType.Application, textSnapshot.Text, Path.GetFileName(filePath))
                .Fields
                .ToDictionary(field => field.FieldKey, field => field.SuggestedValue, StringComparer.OrdinalIgnoreCase);

            var fileReport = new DatasetFileReport
            {
                FileName = Path.GetFileName(filePath),
                Provider = textSnapshot.Provider,
                TextExtractionSucceeded = textSnapshot.IsSuccessful,
                UsedFallback = textSnapshot.IsFallback,
                TextPreview = BuildPreview(textSnapshot.Text),
                ExpectedFields = annotation.Fields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ActualFields = extractedFields
            };

            foreach (var expectedField in fileReport.ExpectedFields)
            {
                if (!fileReport.ActualFields.TryGetValue(expectedField.Key, out var actualValue))
                {
                    fileReport.MissingFields.Add(expectedField.Key);
                    continue;
                }

                if (ValuesMatch(expectedField.Key, expectedField.Value, actualValue))
                {
                    fileReport.MatchedFields.Add(expectedField.Key);
                }
                else
                {
                    fileReport.MismatchedFields.Add(new DatasetFieldMismatch
                    {
                        FieldKey = expectedField.Key,
                        ExpectedValue = expectedField.Value,
                        ActualValue = actualValue
                    });
                }
            }

            report.Files.Add(fileReport);
        }

        report.TotalFiles = report.Files.Count;
        report.SuccessfulExtractions = report.Files.Count(file => file.TextExtractionSucceeded);
        report.FallbackExtractions = report.Files.Count(file => file.UsedFallback);
        report.TotalExpectedFields = report.Files.Sum(file => file.ExpectedFields.Count);
        report.TotalMatchedFields = report.Files.Sum(file => file.MatchedFields.Count);
        report.TotalMissingFields = report.Files.Sum(file => file.MissingFields.Count);
        report.TotalMismatchedFields = report.Files.Sum(file => file.MismatchedFields.Count);
        report.FieldRecall = report.TotalExpectedFields == 0
            ? 0m
            : Math.Round((decimal)report.TotalMatchedFields / report.TotalExpectedFields, 4);

        return report;
    }

    private static async Task<TextSnapshot> ExtractTextAsync(
        string filePath,
        DocumentTextExtractionService textService,
        WindowsOcrService ocrService)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        if (ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            var ocr = await ocrService.ExtractTextAsync(filePath, fileName);
            var transcriptSnapshot = TryLoadManualTranscript(filePath, ocr.IsFallback, ocr.ExtractedText);
            if (transcriptSnapshot is not null)
                return transcriptSnapshot;

            return new TextSnapshot(ocr.IsSuccessful, ocr.IsFallback, ocr.Provider ?? "ocr", ocr.ExtractedText ?? string.Empty);
        }

        var extraction = await textService.ExtractTextAsync(filePath, fileName);
        var manualSnapshot = TryLoadManualTranscript(filePath, extraction.IsFallback, extraction.ExtractedText);
        if (manualSnapshot is not null)
            return manualSnapshot;

        return new TextSnapshot(extraction.IsSuccessful, extraction.IsFallback, extraction.Provider ?? "text", extraction.ExtractedText ?? string.Empty);
    }

    private static TextSnapshot? TryLoadManualTranscript(string filePath, bool usedFallback, string extractedText)
    {
        if (!usedFallback)
            return null;

        var repoRoot = ResolveRepoRoot();
        var transcriptPath = Path.Combine(
            repoRoot,
            "artifacts",
            "dataset-evaluation",
            "manual-transcripts",
            Path.GetFileNameWithoutExtension(filePath) + ".txt");

        if (!File.Exists(transcriptPath))
            return null;

        var transcriptText = File.ReadAllText(transcriptPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(transcriptText))
            return null;

        return new TextSnapshot(true, false, "manual-transcript", transcriptText);
    }

    private static bool ValuesMatch(string fieldKey, string expected, string actual)
    {
        var normalizedExpected = NormalizeValue(expected);
        var normalizedActual = NormalizeValue(actual);

        if (string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
            return true;

        if (fieldKey.EndsWith("_text", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedActual.Contains(normalizedExpected, StringComparison.Ordinal) ||
                   normalizedExpected.Contains(normalizedActual, StringComparison.Ordinal);
        }

        return normalizedActual.Contains(normalizedExpected, StringComparison.Ordinal) ||
               normalizedExpected.Contains(normalizedActual, StringComparison.Ordinal);
    }

    private static string NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Replace('ё', 'е');
        return Regex.Replace(normalized, @"\s+", " ");
    }

    private static string BuildPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = Regex.Replace(value, @"\s+", " ").Trim();
        return normalized.Length <= 220 ? normalized : normalized[..220] + "...";
    }

    private static string BuildMarkdown(ApplicationDatasetEvaluationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Application Dataset Evaluation");
        sb.AppendLine();
        sb.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Dataset root: `{report.DatasetRoot}`");

        foreach (var split in report.Splits)
        {
            sb.AppendLine();
            sb.AppendLine($"## {split.Split}");
            sb.AppendLine();
            sb.AppendLine($"- Files: {split.TotalFiles}");
            sb.AppendLine($"- Successful extractions: {split.SuccessfulExtractions}");
            sb.AppendLine($"- Fallback extractions: {split.FallbackExtractions}");
            sb.AppendLine($"- Matched fields: {split.TotalMatchedFields}/{split.TotalExpectedFields}");
            sb.AppendLine($"- Field recall: {split.FieldRecall:P1}");
            sb.AppendLine();

            foreach (var file in split.Files)
            {
                sb.AppendLine($"### {file.FileName}");
                sb.AppendLine();
                sb.AppendLine($"- Provider: `{file.Provider}`");
                sb.AppendLine($"- Extraction succeeded: {(file.TextExtractionSucceeded ? "yes" : "no")}");
                sb.AppendLine($"- Fallback: {(file.UsedFallback ? "yes" : "no")}");
                sb.AppendLine($"- Matched fields: {file.MatchedFields.Count}/{file.ExpectedFields.Count}");

                if (file.MissingFields.Count > 0)
                    sb.AppendLine($"- Missing: {string.Join(", ", file.MissingFields)}");

                if (file.MismatchedFields.Count > 0)
                {
                    sb.AppendLine("- Mismatches:");
                    foreach (var mismatch in file.MismatchedFields)
                        sb.AppendLine($"  - `{mismatch.FieldKey}` expected `{mismatch.ExpectedValue}` actual `{mismatch.ActualValue}`");
                }

                if (!string.IsNullOrWhiteSpace(file.TextPreview))
                    sb.AppendLine($"- Text preview: `{file.TextPreview}`");

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string ResolveDatasetRoot()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("DOCUMENTFLOWAPP_DATASET_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Path.GetFullPath(fromEnvironment);

        var repoRoot = ResolveRepoRoot();
        return Path.GetFullPath(Path.Combine(repoRoot, "..", "DocumentFlowAppDataset", "dataset", "Application"));
    }

    private static string ResolveRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            if (File.Exists(Path.Combine(current, "DocumentFlowApp.sln")))
                return current;

            var parent = Directory.GetParent(current);
            if (parent is null)
                break;

            current = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed record TextSnapshot(bool IsSuccessful, bool IsFallback, string Provider, string Text);

    private sealed class ApplicationAnnotation
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; init; }

        [JsonPropertyName("documentType")]
        public string? DocumentType { get; init; }

        [JsonPropertyName("fields")]
        public Dictionary<string, string>? Fields { get; init; }
    }

    private sealed class ApplicationDatasetEvaluationReport
    {
        public DateTime GeneratedAtUtc { get; init; }
        public string DatasetRoot { get; init; } = string.Empty;
        public List<DatasetSplitReport> Splits { get; init; } = [];
    }

    private sealed class DatasetSplitReport
    {
        public string Split { get; init; } = string.Empty;
        public string FolderPath { get; init; } = string.Empty;
        public int TotalFiles { get; set; }
        public int SuccessfulExtractions { get; set; }
        public int FallbackExtractions { get; set; }
        public int TotalExpectedFields { get; set; }
        public int TotalMatchedFields { get; set; }
        public int TotalMissingFields { get; set; }
        public int TotalMismatchedFields { get; set; }
        public decimal FieldRecall { get; set; }
        public List<DatasetFileReport> Files { get; init; } = [];
    }

    private sealed class DatasetFileReport
    {
        public string FileName { get; init; } = string.Empty;
        public string Provider { get; init; } = string.Empty;
        public bool TextExtractionSucceeded { get; init; }
        public bool UsedFallback { get; init; }
        public string TextPreview { get; init; } = string.Empty;
        public Dictionary<string, string> ExpectedFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ActualFields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> MatchedFields { get; init; } = [];
        public List<string> MissingFields { get; init; } = [];
        public List<DatasetFieldMismatch> MismatchedFields { get; init; } = [];
    }

    private sealed class DatasetFieldMismatch
    {
        public string FieldKey { get; init; } = string.Empty;
        public string ExpectedValue { get; init; } = string.Empty;
        public string ActualValue { get; init; } = string.Empty;
    }
}
