using System.Diagnostics;
using System.Text;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentFlowApp.Infrastructure.Services;

public sealed class WindowsOcrService : IOcrService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    private readonly ILogger<WindowsOcrService> _logger;

    public WindowsOcrService(ILogger<WindowsOcrService> logger)
    {
        _logger = logger;
    }

    public async Task<OcrExtractionResult> ExtractTextAsync(
        string physicalPath,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!SupportedExtensions.Contains(extension ?? string.Empty))
        {
            return new OcrExtractionResult
            {
                IsSuccessful = false,
                Provider = "unsupported",
                Summary = "OCR доступен только для PNG/JPEG."
            };
        }

        if (!File.Exists(physicalPath))
        {
            return new OcrExtractionResult
            {
                IsSuccessful = false,
                Provider = "missing-file",
                Summary = "Файл для OCR не найден."
            };
        }

        try
        {
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "ocr-image.ps1");
            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("OCR script was not found at {ScriptPath}", scriptPath);
                return BuildFallbackResult(originalFileName, "ocr-script-missing");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ImagePath \"{physicalPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return BuildFallbackResult(originalFileName, "ocr-process-start-failed");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var extractedText = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(extractedText))
            {
                return new OcrExtractionResult
                {
                    IsSuccessful = true,
                    Provider = "windows-ocr",
                    ExtractedText = extractedText,
                    ConfidenceScore = EstimateConfidence(extractedText),
                    Summary = "Текст извлечён из изображения встроенным Windows OCR."
                };
            }

            if (!string.IsNullOrWhiteSpace(stderr))
                _logger.LogInformation("Windows OCR returned no text for {FileName}: {Error}", originalFileName, stderr);

            return BuildFallbackResult(originalFileName, "filename-fallback");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Windows OCR failed for {FileName}", originalFileName);
            return BuildFallbackResult(originalFileName, "filename-fallback");
        }
    }

    private static OcrExtractionResult BuildFallbackResult(string originalFileName, string provider)
    {
        var extractedText = NormalizeFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return new OcrExtractionResult
            {
                IsSuccessful = false,
                IsFallback = true,
                Provider = provider,
                Summary = "Не удалось извлечь текст из изображения."
            };
        }

        return new OcrExtractionResult
        {
            IsSuccessful = true,
            IsFallback = true,
            Provider = provider,
            ExtractedText = extractedText,
            ConfidenceScore = 0.51m,
            Summary = "Вместо OCR использованы текстовые признаки из имени файла."
        };
    }

    private static string NormalizeFileName(string originalFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(baseName))
            return string.Empty;

        var normalized = baseName
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('.', ' ')
            .Trim();

        while (normalized.Contains("  ", StringComparison.Ordinal))
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);

        return normalized;
    }

    private static decimal EstimateConfidence(string extractedText)
    {
        var length = extractedText.Length;
        if (length >= 120)
            return 0.83m;
        if (length >= 50)
            return 0.76m;
        if (length >= 20)
            return 0.68m;
        return 0.58m;
    }
}
