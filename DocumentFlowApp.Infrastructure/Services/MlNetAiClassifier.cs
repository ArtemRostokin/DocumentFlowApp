using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace DocumentFlowApp.Infrastructure.Services;

public sealed class MlNetAiClassifier : IAiClassifier
{
    private static readonly IReadOnlyDictionary<DocumentType, TypeProfile> Profiles =
        new Dictionary<DocumentType, TypeProfile>
        {
            [DocumentType.Contract] = new(
                "Юридический документ. Проверьте номер, дату, контрагента и сумму договора.",
                ["incoming", "contract", "legal"],
                ["договор", "dogovor", "contract", "agreement"],
                ["контрагент", "предмет", "сумма", "аренда", "поставки"]),
            [DocumentType.Invoice] = new(
                "Финансовый документ. Проверьте реквизиты оплаты, сумму и поставщика.",
                ["incoming", "invoice", "finance"],
                ["счет", "счёт", "invoice", "schet", "bill"],
                ["оплата", "поставщик", "инн", "кпп", "сумма"]),
            [DocumentType.Report] = new(
                "Отчетный документ. Проверьте период, показатели и итоговые значения.",
                ["incoming", "report", "analytics"],
                ["отчет", "отчёт", "report"],
                ["итоги", "показатели", "результаты", "аналитика"]),
            [DocumentType.Order] = new(
                "Распорядительный документ. Проверьте дату, номер и предмет приказа.",
                ["incoming", "order", "internal"],
                ["приказ", "order"],
                ["назначить", "утвердить", "распоряжение", "сотрудник"]),
            [DocumentType.Application] = new(
                "Заявление или запрос. Проверьте автора, дату и текст обращения.",
                ["incoming", "application", "request"],
                ["заявление", "application", "request"],
                ["прошу", "заявитель", "отпуск", "сотрудник"]),
            [DocumentType.Act] = new(
                "Акт выполненных работ или услуг. Проверьте стороны, дату и сумму.",
                ["incoming", "act", "closing"],
                ["акт", "act"],
                ["выполненных работ", "оказанных услуг", "приемка", "приемки"]),
            [DocumentType.Other] = new(
                "Недостаточно уверенного сигнала для автоматической классификации, проверьте тип вручную.",
                ["incoming", "needs-review", "other"],
                ["файл", "document", "scan"],
                ["скан", "вложение", "документ"])
        };

    private readonly string _modelPath;
    private readonly Lazy<ModelArtifacts> _artifacts;

    public MlNetAiClassifier(string? modelPath = null)
    {
        _modelPath = string.IsNullOrWhiteSpace(modelPath) ? GetDefaultModelPath() : modelPath;
        _artifacts = new Lazy<ModelArtifacts>(() => CreateArtifacts(_modelPath), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public AiClassificationResult ClassifyIncomingDocument(string fileName, string? extractedText = null)
    {
        var normalizedText = BuildInputText(fileName, extractedText);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return BuildLowConfidenceResult("Недостаточно данных для уверенной классификации.");
        }

        var prediction = Predict(normalizedText);
        var predictedType = ResolvePredictedType(normalizedText, prediction.Score);
        var confidence = CalculateConfidence(prediction, normalizedText, predictedType);

        if (predictedType == DocumentType.Other || confidence < 0.60m)
        {
            return BuildLowConfidenceResult("Сигналов недостаточно, тип лучше подтвердить вручную.");
        }

        var profile = Profiles[predictedType];
        var tags = confidence >= 0.85m
            ? profile.Tags.Concat(["ai-auto-classified"]).ToArray()
            : profile.Tags.Concat(["ai-needs-review"]).ToArray();

        return new AiClassificationResult
        {
            SuggestedType = predictedType,
            ConfidenceScore = confidence,
            Summary = profile.Summary,
            SuggestedTags = tags
        };
    }

    public IReadOnlyList<AiFieldSuggestionResult> BuildSuggestions(Document document)
    {
        var classification = ClassifyIncomingDocument(document.Title ?? string.Empty, document.ExtractedText);
        var seed = BuildSeed(document);
        var rng = new Random(seed);

        decimal Confidence(decimal baseValue, int varianceMax = 6)
        {
            var variance = rng.Next(0, varianceMax + 1) / 100m;
            return Math.Round(Math.Clamp(baseValue - variance, 0.45m, 0.98m), 2, MidpointRounding.AwayFromZero);
        }

        var title = string.IsNullOrWhiteSpace(document.Title)
            ? $"Документ #{document.DocumentId}"
            : document.Title!;
        var description = string.IsNullOrWhiteSpace(document.ExtractedText)
            ? classification.Summary
            : document.ExtractedText!;
        var dueDate = document.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? DateTime.UtcNow.Date.AddDays(classification.ShouldAutoAssignType ? 5 : 10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var priority = document.Priority?.ToString(CultureInfo.InvariantCulture)
            ?? (classification.SuggestedType is DocumentType.Invoice or DocumentType.Contract ? "3" : "2");
        var tags = string.IsNullOrWhiteSpace(document.Tags)
            ? string.Join(',', classification.SuggestedTags)
            : document.Tags!;

        return
        [
            new AiFieldSuggestionResult
            {
                FieldKey = "type",
                Label = "Тип",
                SuggestedValue = classification.SuggestedType.ToString(),
                ConfidenceScore = classification.ConfidenceScore
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "duedate",
                Label = "Срок исполнения",
                SuggestedValue = dueDate,
                ConfidenceScore = Confidence(classification.ShouldAutoAssignType ? 0.84m : 0.67m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "title",
                Label = "Название",
                SuggestedValue = title,
                ConfidenceScore = Confidence(0.88m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "description",
                Label = "Описание",
                SuggestedValue = description,
                ConfidenceScore = Confidence(string.IsNullOrWhiteSpace(document.ExtractedText) ? 0.63m : 0.86m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "priority",
                Label = "Приоритет",
                SuggestedValue = priority,
                ConfidenceScore = Confidence(classification.SuggestedType == DocumentType.Invoice ? 0.79m : 0.68m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "tags",
                Label = "Теги",
                SuggestedValue = tags,
                ConfidenceScore = Confidence(0.72m)
            }
        ];
    }

    private ModelPrediction Predict(string normalizedText)
    {
        var artifacts = _artifacts.Value;
        var engine = artifacts.Context.Model.CreatePredictionEngine<ModelInput, ModelPrediction>(artifacts.Transformer);
        return engine.Predict(new ModelInput { Text = normalizedText });
    }

    private static ModelArtifacts CreateArtifacts(string modelPath)
    {
        var context = new MLContext(seed: 42);
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

        if (File.Exists(modelPath))
        {
            try
            {
                using var readStream = File.OpenRead(modelPath);
                var loadedModel = context.Model.Load(readStream, out var schema);
                return new ModelArtifacts(context, loadedModel, schema, GetLabels(loadedModel, schema), modelPath);
            }
            catch
            {
                // If the persisted model is broken, retrain from the bundled baseline samples.
            }
        }

        var data = context.Data.LoadFromEnumerable(GetTrainingSamples());
        var pipeline = context.Transforms.Conversion.MapValueToKey(nameof(ModelInput.Label))
            .Append(context.Transforms.Text.FeaturizeText(nameof(ModelFeatures.Features), nameof(ModelInput.Text)))
            .Append(context.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                labelColumnName: nameof(ModelInput.Label),
                featureColumnName: nameof(ModelFeatures.Features)));

        var transformer = pipeline.Fit(data);

        using (var writeStream = File.Create(modelPath))
        {
            context.Model.Save(transformer, data.Schema, writeStream);
        }

        return new ModelArtifacts(context, transformer, data.Schema, GetLabels(transformer, data.Schema), modelPath);
    }

    private static string BuildInputText(string fileName, string? extractedText)
    {
        var fileStem = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        var raw = $"{fileStem} {extractedText ?? string.Empty}";
        var normalized = Regex.Replace(raw.ToLowerInvariant(), @"[^\p{L}\p{N}\s]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static decimal CalculateConfidence(ModelPrediction prediction, string normalizedText, DocumentType predictedType)
    {
        var probability = GetTopProbability(prediction.Score);
        var evidence = CalculateEvidenceScore(normalizedText, predictedType);
        var confidence = (Math.Max(probability, evidence) * 0.60m) + (evidence * 0.40m);

        if (predictedType != DocumentType.Other && evidence >= 0.84m)
            confidence = Math.Max(confidence, 0.88m);

        if (predictedType != DocumentType.Other && evidence < 0.50m)
            confidence = Math.Min(confidence, 0.58m);

        return Math.Round(Math.Clamp(confidence, 0.40m, 0.97m), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateEvidenceScore(string normalizedText, DocumentType predictedType)
    {
        if (!Profiles.TryGetValue(predictedType, out var profile))
            return 0.42m;

        var tokens = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return 0.42m;

        var primaryHits = CountMatches(normalizedText, profile.PrimaryKeywords);
        var secondaryHits = CountMatches(normalizedText, profile.SecondaryKeywords);

        if (primaryHits >= 2 || (primaryHits >= 1 && secondaryHits >= 1))
            return 0.92m;

        if (primaryHits == 1)
            return 0.84m;

        if (secondaryHits >= 2)
            return 0.71m;

        if (secondaryHits == 1)
            return 0.64m;

        return 0.42m;
    }

    private static int CountMatches(string normalizedText, IEnumerable<string> variants)
        => variants.Count(variant => normalizedText.Contains(variant, StringComparison.OrdinalIgnoreCase));

    private static decimal GetTopProbability(float[]? scores)
    {
        if (scores is null || scores.Length == 0)
            return 0.42m;

        var exponentials = scores.Select(score => Math.Exp(score)).ToArray();
        var total = exponentials.Sum();
        if (total <= 0d)
            return 0.42m;

        var max = exponentials.Max() / total;
        return (decimal)max;
    }

    private DocumentType GetPredictedType(float[]? scores)
    {
        var labels = _artifacts.Value.Labels;
        if (scores is null || scores.Length == 0 || labels.Length == 0)
            return DocumentType.Other;

        var bestIndex = 0;
        var bestScore = scores[0];

        for (var i = 1; i < scores.Length; i++)
        {
            if (scores[i] <= bestScore)
                continue;

            bestScore = scores[i];
            bestIndex = i;
        }

        if (bestIndex >= labels.Length)
            return DocumentType.Other;

        return Enum.TryParse<DocumentType>(labels[bestIndex], ignoreCase: true, out var type)
            ? type
            : DocumentType.Other;
    }

    private DocumentType ResolvePredictedType(string normalizedText, float[]? scores)
    {
        var mlPredictedType = GetPredictedType(scores);
        if (mlPredictedType != DocumentType.Other)
            return mlPredictedType;

        var bestType = DocumentType.Other;
        var bestEvidence = 0.42m;

        foreach (var candidate in Profiles.Keys.Where(static type => type != DocumentType.Other))
        {
            var evidence = CalculateEvidenceScore(normalizedText, candidate);
            if (evidence <= bestEvidence)
                continue;

            bestEvidence = evidence;
            bestType = candidate;
        }

        if (bestEvidence >= 0.64m)
            return bestType;

        return DocumentType.Other;
    }

    private static string[] GetLabels(ITransformer transformer, DataViewSchema inputSchema)
    {
        var outputSchema = transformer.GetOutputSchema(inputSchema);
        var scoreColumn = outputSchema.FirstOrDefault(static column => column.Name == "Score");
        if (string.IsNullOrWhiteSpace(scoreColumn.Name))
            return [];

        VBuffer<ReadOnlyMemory<char>> buffer = default;
        scoreColumn.Annotations.GetValue("SlotNames", ref buffer);
        return buffer.DenseValues().Select(static item => item.ToString()).ToArray();
    }

    private static AiClassificationResult BuildLowConfidenceResult(string summary)
        => new()
        {
            SuggestedType = DocumentType.Other,
            ConfidenceScore = 0.42m,
            Summary = summary,
            SuggestedTags = ["incoming", "needs-review", "ai-low-confidence"]
        };

    private static int BuildSeed(Document document)
    {
        var seed = document.DocumentId;

        unchecked
        {
            foreach (var ch in document.FileHash ?? string.Empty)
                seed = (seed * 31) + ch;

            foreach (var ch in document.Title ?? string.Empty)
                seed = (seed * 17) + ch;
        }

        return seed;
    }

    private static string GetDefaultModelPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DocumentFlowApp",
            "ml");
        return Path.Combine(root, "document-type-classifier.zip");
    }

    private static IReadOnlyList<ModelInput> GetTrainingSamples()
    {
        return
        [
            new() { Label = nameof(DocumentType.Contract), Text = "договор поставки номер дата контрагент сумма предмет договора" },
            new() { Label = nameof(DocumentType.Contract), Text = "contract agreement supplier contract amount counterparty legal terms" },
            new() { Label = nameof(DocumentType.Contract), Text = "договор аренды помещения срок действия сумма арендодатель арендатор" },
            new() { Label = nameof(DocumentType.Contract), Text = "dogovor оказание услуг цена договора заказчик исполнитель" },
            new() { Label = nameof(DocumentType.Contract), Text = "договор купли продажи условия поставки контрагент инн сумма" },

            new() { Label = nameof(DocumentType.Invoice), Text = "счет на оплату поставщик сумма инн кпп реквизиты оплаты" },
            new() { Label = nameof(DocumentType.Invoice), Text = "invoice bill payment details supplier total vat account" },
            new() { Label = nameof(DocumentType.Invoice), Text = "счёт оплата товара банковские реквизиты получатель сумма" },
            new() { Label = nameof(DocumentType.Invoice), Text = "schet postavshik oplata summa inn kpp" },
            new() { Label = nameof(DocumentType.Invoice), Text = "счет фактура оплата поставщик сумма к оплате" },

            new() { Label = nameof(DocumentType.Report), Text = "отчет результаты показатели период анализ выводы" },
            new() { Label = nameof(DocumentType.Report), Text = "report analytics performance indicators monthly summary" },
            new() { Label = nameof(DocumentType.Report), Text = "отчёт по продажам показатели итоги месяц квартал" },
            new() { Label = nameof(DocumentType.Report), Text = "служебный отчет результат работы подразделения показатели" },
            new() { Label = nameof(DocumentType.Report), Text = "report итоговые значения статистика аналитика" },

            new() { Label = nameof(DocumentType.Order), Text = "приказ назначить ответственным утвердить распоряжение по организации" },
            new() { Label = nameof(DocumentType.Order), Text = "order internal instruction approve assign employee manager" },
            new() { Label = nameof(DocumentType.Order), Text = "приказ о назначении сотрудника дата номер приказа" },
            new() { Label = nameof(DocumentType.Order), Text = "распоряжение руководителя утвердить график приказ" },
            new() { Label = nameof(DocumentType.Order), Text = "order on staffing approval company directive" },

            new() { Label = nameof(DocumentType.Application), Text = "заявление прошу предоставить отпуск сотрудник дата подпись" },
            new() { Label = nameof(DocumentType.Application), Text = "application request employee asks for leave" },
            new() { Label = nameof(DocumentType.Application), Text = "заявление на отпуск прошу согласовать заявление работника" },
            new() { Label = nameof(DocumentType.Application), Text = "request form applicant department text of request" },
            new() { Label = nameof(DocumentType.Application), Text = "заявление сотрудника прошу перевести выдать справку" },

            new() { Label = nameof(DocumentType.Act), Text = "акт выполненных работ оказанных услуг приемка сумма дата" },
            new() { Label = nameof(DocumentType.Act), Text = "act acceptance completed works rendered services total" },
            new() { Label = nameof(DocumentType.Act), Text = "акт приемки оказанных услуг заказчик исполнитель" },
            new() { Label = nameof(DocumentType.Act), Text = "act of completed work acceptance certificate amount" },
            new() { Label = nameof(DocumentType.Act), Text = "акт сдачи приемки работ дата номер сумма" },

            new() { Label = nameof(DocumentType.Other), Text = "скан документа вложение без явных реквизитов" },
            new() { Label = nameof(DocumentType.Other), Text = "misc file attachment scan image document" },
            new() { Label = nameof(DocumentType.Other), Text = "черновой файл заметки изображение без типа" },
            new() { Label = nameof(DocumentType.Other), Text = "scan image uploaded file without clear metadata" },
            new() { Label = nameof(DocumentType.Other), Text = "прочий документ без выраженных признаков типа" }
        ];
    }

    private sealed record TypeProfile(
        string Summary,
        string[] Tags,
        string[] PrimaryKeywords,
        string[] SecondaryKeywords);

    private sealed record ModelArtifacts(
        MLContext Context,
        ITransformer Transformer,
        DataViewSchema InputSchema,
        string[] Labels,
        string ModelPath);

    private sealed class ModelInput
    {
        [LoadColumn(0)]
        public string Label { get; init; } = string.Empty;

        [LoadColumn(1)]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class ModelFeatures
    {
        [VectorType]
        public float[] Features { get; init; } = [];
    }

    private sealed class ModelPrediction
    {
        public float[] Score { get; set; } = [];
    }
}
