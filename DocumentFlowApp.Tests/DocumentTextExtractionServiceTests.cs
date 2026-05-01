using System.IO.Compression;
using System.Text;
using DocumentFlowApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentFlowApp.Tests;

public sealed class DocumentTextExtractionServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_Reads_Text_From_Docx()
    {
        var service = new DocumentTextExtractionService(NullLogger<DocumentTextExtractionService>.Instance);
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");

        try
        {
            CreateDocx(tempFile, "Договор поставки", "Контрагент ООО Ромашка");

            var result = await service.ExtractTextAsync(tempFile, "contract.docx");

            Assert.True(result.IsSuccessful);
            Assert.False(result.IsFallback);
            Assert.Equal("docx-xml", result.Provider);
            Assert.Contains("Договор поставки", result.ExtractedText);
            Assert.Contains("Контрагент ООО Ромашка", result.ExtractedText);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_Falls_Back_To_FileName_When_Docx_IsBroken()
    {
        var service = new DocumentTextExtractionService(NullLogger<DocumentTextExtractionService>.Instance);
        var tempFile = Path.Combine(Path.GetTempPath(), $"dogovor_postavki_{Guid.NewGuid():N}.docx");

        try
        {
            await File.WriteAllBytesAsync(tempFile, [1, 2, 3, 4]);

            var result = await service.ExtractTextAsync(tempFile, "dogovor_postavki.docx");

            Assert.True(result.IsSuccessful);
            Assert.True(result.IsFallback);
            Assert.Equal("docx-filename-fallback", result.Provider);
            Assert.Equal("dogovor postavki", result.ExtractedText);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static void CreateDocx(string path, params string[] paragraphs)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("word/document.xml");

        var xml = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                {string.Join(Environment.NewLine, paragraphs.Select(p => $"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(p)}</w:t></w:r></w:p>"))}
              </w:body>
            </w:document>
            """;

        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(xml);
    }
}
