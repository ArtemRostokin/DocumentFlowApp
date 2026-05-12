using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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
            var inProcessText = await TryExtractTextWithWindowsRuntimeAsync(physicalPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(inProcessText))
            {
                return new OcrExtractionResult
                {
                    IsSuccessful = true,
                    Provider = "windows-ocr-inprocess",
                    ExtractedText = inProcessText,
                    ConfidenceScore = EstimateConfidence(inProcessText),
                    Summary = "Текст извлечён из изображения встроенным Windows OCR."
                };
            }

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "ocr-image.ps1");
            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("OCR script was not found at {ScriptPath}", scriptPath);
                return BuildFallbackResult(originalFileName, "ocr-script-missing");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Sta -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ImagePath \"{physicalPath}\"",
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

    private async Task<string?> TryExtractTextWithWindowsRuntimeAsync(string physicalPath, CancellationToken cancellationToken)
    {
        try
        {
            var storageFileType = Type.GetType("Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime");
            var fileAccessModeType = Type.GetType("Windows.Storage.FileAccessMode, Windows.Storage, ContentType=WindowsRuntime");
            var bitmapDecoderType = Type.GetType("Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType=WindowsRuntime");
            var softwareBitmapType = Type.GetType("Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType=WindowsRuntime");
            var bitmapPixelFormatType = Type.GetType("Windows.Graphics.Imaging.BitmapPixelFormat, Windows.Graphics.Imaging, ContentType=WindowsRuntime");
            var bitmapAlphaModeType = Type.GetType("Windows.Graphics.Imaging.BitmapAlphaMode, Windows.Graphics.Imaging, ContentType=WindowsRuntime");
            var ocrEngineType = Type.GetType("Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType=WindowsRuntime");
            var languageType = Type.GetType("Windows.Globalization.Language, Windows.Globalization, ContentType=WindowsRuntime");

            if (storageFileType is null ||
                fileAccessModeType is null ||
                bitmapDecoderType is null ||
                softwareBitmapType is null ||
                bitmapPixelFormatType is null ||
                bitmapAlphaModeType is null ||
                ocrEngineType is null)
            {
                return null;
            }

            dynamic fileOperation = storageFileType
                .GetMethod("GetFileFromPathAsync", [typeof(string)])!
                .Invoke(null, [physicalPath])!;
            dynamic storageFile = await AwaitWinRtAsync(fileOperation, cancellationToken);

            var readMode = Enum.Parse(fileAccessModeType, "Read");
            dynamic openOperation = storageFile.OpenAsync((dynamic)readMode);
            dynamic stream = await AwaitWinRtAsync(openOperation, cancellationToken);

            dynamic decoderOperation = bitmapDecoderType
                .GetMethod("CreateAsync")!
                .Invoke(null, [stream])!;
            dynamic decoder = await AwaitWinRtAsync(decoderOperation, cancellationToken);

            dynamic bitmapOperation = decoder.GetSoftwareBitmapAsync();
            dynamic bitmap = await AwaitWinRtAsync(bitmapOperation, cancellationToken);

            var bgra8 = Enum.Parse(bitmapPixelFormatType, "Bgra8");
            var premultiplied = Enum.Parse(bitmapAlphaModeType, "Premultiplied");
            bitmap = softwareBitmapType
                .GetMethod("Convert", [softwareBitmapType, bitmapPixelFormatType, bitmapAlphaModeType])!
                .Invoke(null, [bitmap, bgra8, premultiplied])!;

            dynamic? engine = ocrEngineType
                .GetMethod("TryCreateFromUserProfileLanguages")!
                .Invoke(null, null);

            if (engine is null && languageType is not null)
            {
                var language = Activator.CreateInstance(languageType, "ru-RU");
                engine = ocrEngineType
                    .GetMethod("TryCreateFromLanguage", [languageType])!
                    .Invoke(null, [language]);
            }

            if (engine is null)
                return null;

            dynamic recognitionOperation = engine.RecognizeAsync(bitmap);
            dynamic recognitionResult = await AwaitWinRtAsync(recognitionOperation, cancellationToken);
            var rawText = recognitionResult.Text as string;
            var normalized = NormalizeWhitespace(rawText);

            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "In-process WinRT OCR failed for {PhysicalPath}", physicalPath);
            return null;
        }
    }

    private static async Task<dynamic?> AwaitWinRtAsync(dynamic operation, CancellationToken cancellationToken)
    {
        var extensionsType = Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime");
        if (extensionsType is null)
            throw new InvalidOperationException("System.Runtime.WindowsRuntime is unavailable.");

        var asTaskMethod = extensionsType
            .GetMethods()
            .FirstOrDefault(method =>
                method.Name == "AsTask" &&
                method.GetParameters().Length == 2 &&
                method.GetParameters()[1].ParameterType == typeof(CancellationToken));

        if (asTaskMethod is null)
            throw new InvalidOperationException("WinRT AsTask overload was not found.");

        dynamic task = asTaskMethod.Invoke(null, [operation, cancellationToken])!;
        await task;
        return task.GetAwaiter().GetResult();
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

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value, @"\s+", " ").Trim();
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
