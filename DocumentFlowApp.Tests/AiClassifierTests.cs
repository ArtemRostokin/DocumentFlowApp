using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Infrastructure.Services;

namespace DocumentFlowApp.Tests;

public sealed class AiClassifierTests
{
    private readonly MlNetAiClassifier _classifier;

    public AiClassifierTests()
    {
        var modelPath = Path.Combine(Path.GetTempPath(), "DocumentFlowApp.Tests", $"{Guid.NewGuid():N}.zip");
        _classifier = new MlNetAiClassifier(modelPath);
    }

    [Fact]
    public void ClassifyIncomingDocument_Returns_HighConfidence_For_ObviousContract()
    {
        var result = _classifier.ClassifyIncomingDocument("dogovor_postavki.pdf");

        Assert.Equal(DocumentType.Contract, result.SuggestedType);
        Assert.True(result.ShouldAutoAssignType);
        Assert.True(result.ConfidenceScore >= 0.85m);
    }

    [Fact]
    public void ClassifyIncomingDocument_Returns_ManualReview_For_WeakTextSignal()
    {
        var result = _classifier.ClassifyIncomingDocument(
            "scan_001.png",
            "Поставщик просит проверить реквизиты оплаты и приложить подтверждение.");

        Assert.Equal(DocumentType.Invoice, result.SuggestedType);
        Assert.True(result.RequiresManualReview);
        Assert.InRange(result.ConfidenceScore, 0.60m, 0.84m);
    }

    [Fact]
    public void ClassifyIncomingDocument_Returns_Other_For_LowConfidence()
    {
        var result = _classifier.ClassifyIncomingDocument("image_001.png", "Случайный текст без явных признаков документа.");

        Assert.Equal(DocumentType.Other, result.SuggestedType);
        Assert.False(result.ShouldAutoAssignType);
        Assert.False(result.RequiresManualReview);
        Assert.True(result.ConfidenceScore < 0.60m);
    }

    [Fact]
    public void BuildSuggestions_Uses_Classifier_Result_For_TypeSuggestion()
    {
        var document = new Document
        {
            DocumentId = 77,
            Title = "invoice_april",
            ExtractedText = "Счет на оплату",
            DocumentType = DocumentType.Other.ToString()
        };

        var suggestions = _classifier.BuildSuggestions(document);
        var typeSuggestion = Assert.Single(suggestions, x => x.FieldKey == "type");

        Assert.Equal(DocumentType.Invoice.ToString(), typeSuggestion.SuggestedValue);
        Assert.True(typeSuggestion.ConfidenceScore >= 0.85m);
    }
}
