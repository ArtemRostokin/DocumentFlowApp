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
                "Р®СЂРёРґРёС‡РµСЃРєРёР№ РґРѕРєСѓРјРµРЅС‚. РџСЂРѕРІРµСЂСЊС‚Рµ РЅРѕРјРµСЂ, РґР°С‚Сѓ, РєРѕРЅС‚СЂР°РіРµРЅС‚Р° Рё СЃСѓРјРјСѓ РґРѕРіРѕРІРѕСЂР°.",
                ["incoming", "contract", "legal"],
                ["РґРѕРіРѕРІРѕСЂ", "dogovor", "contract", "agreement"],
                ["РєРѕРЅС‚СЂР°РіРµРЅС‚", "РїСЂРµРґРјРµС‚", "СЃСѓРјРјР°", "Р°СЂРµРЅРґР°", "РїРѕСЃС‚Р°РІРєРё"]),
            [DocumentType.Invoice] = new(
                "Р¤РёРЅР°РЅСЃРѕРІС‹Р№ РґРѕРєСѓРјРµРЅС‚. РџСЂРѕРІРµСЂСЊС‚Рµ СЂРµРєРІРёР·РёС‚С‹ РѕРїР»Р°С‚С‹, СЃСѓРјРјСѓ Рё РїРѕСЃС‚Р°РІС‰РёРєР°.",
                ["incoming", "invoice", "finance"],
                ["СЃС‡РµС‚", "СЃС‡С‘С‚", "invoice", "schet", "bill"],
                ["РѕРїР»Р°С‚Р°", "РїРѕСЃС‚Р°РІС‰РёРє", "РёРЅРЅ", "РєРїРї", "СЃСѓРјРјР°"]),
            [DocumentType.Report] = new(
                "РћС‚С‡РµС‚РЅС‹Р№ РґРѕРєСѓРјРµРЅС‚. РџСЂРѕРІРµСЂСЊС‚Рµ РїРµСЂРёРѕРґ, РїРѕРєР°Р·Р°С‚РµР»Рё Рё РёС‚РѕРіРѕРІС‹Рµ Р·РЅР°С‡РµРЅРёСЏ.",
                ["incoming", "report", "analytics"],
                ["РѕС‚С‡РµС‚", "РѕС‚С‡С‘С‚", "report"],
                ["РёС‚РѕРіРё", "РїРѕРєР°Р·Р°С‚РµР»Рё", "СЂРµР·СѓР»СЊС‚Р°С‚С‹", "Р°РЅР°Р»РёС‚РёРєР°"]),
            [DocumentType.Order] = new(
                "Р Р°СЃРїРѕСЂСЏРґРёС‚РµР»СЊРЅС‹Р№ РґРѕРєСѓРјРµРЅС‚. РџСЂРѕРІРµСЂСЊС‚Рµ РґР°С‚Сѓ, РЅРѕРјРµСЂ Рё РїСЂРµРґРјРµС‚ РїСЂРёРєР°Р·Р°.",
                ["incoming", "order", "internal"],
                ["РїСЂРёРєР°Р·", "order"],
                ["РЅР°Р·РЅР°С‡РёС‚СЊ", "СѓС‚РІРµСЂРґРёС‚СЊ", "СЂР°СЃРїРѕСЂСЏР¶РµРЅРёРµ", "СЃРѕС‚СЂСѓРґРЅРёРє"]),
            [DocumentType.Application] = new(
                "Р—Р°СЏРІР»РµРЅРёРµ РёР»Рё Р·Р°РїСЂРѕСЃ. РџСЂРѕРІРµСЂСЊС‚Рµ Р°РІС‚РѕСЂР°, РґР°С‚Сѓ Рё С‚РµРєСЃС‚ РѕР±СЂР°С‰РµРЅРёСЏ.",
                ["incoming", "application", "request"],
                ["Р·Р°СЏРІР»РµРЅРёРµ", "application", "request"],
                ["РїСЂРѕС€Сѓ", "Р·Р°СЏРІРёС‚РµР»СЊ", "РѕС‚РїСѓСЃРє", "СЃРѕС‚СЂСѓРґРЅРёРє"]),
            [DocumentType.ServiceMemo] = new(
                "РЎР»СѓР¶РµР±РЅР°СЏ Р·Р°РїРёСЃРєР° РІРЅСѓС‚СЂРµРЅРЅРµРіРѕ С…Р°СЂР°РєС‚РµСЂР°. РџСЂРѕРІРµСЂСЊС‚Рµ РёРЅРёС†РёР°С‚РѕСЂР°, РїРѕРґСЂР°Р·РґРµР»РµРЅРёРµ Рё С‚РµРјСѓ Р·Р°РїРёСЃРєРё.",
                ["incoming", "memo", "internal"],
                ["СЃР»СѓР¶РµР±РЅР°СЏ", "Р·Р°РїРёСЃРєР°", "memo"],
                ["РёРЅРёС†РёР°С‚РѕСЂ", "РїРѕРґСЂР°Р·РґРµР»РµРЅРёРµ", "С‚РµРјР° Р·Р°РїРёСЃРєРё", "РїРѕСЏСЃРЅРµРЅРёРµ"]),
            [DocumentType.PurchaseRequest] = new(
                "Р—Р°СЏРІРєР° РЅР° Р·Р°РєСѓРїРєСѓ. РџСЂРѕРІРµСЂСЊС‚Рµ РїСЂРµРґРјРµС‚ Р·Р°РєСѓРїРєРё, РїР»Р°РЅРѕРІСѓСЋ СЃСѓРјРјСѓ Рё РѕР±РѕСЃРЅРѕРІР°РЅРёРµ.",
                ["incoming", "purchase", "finance"],
                ["Р·Р°РєСѓРїРєР°", "Р·Р°СЏРІРєР°", "purchase request"],
                ["РѕР±РѕСЃРЅРѕРІР°РЅРёРµ", "РїР»Р°РЅРѕРІР°СЏ СЃСѓРјРјР°", "РїСЂРµРґРјРµС‚ Р·Р°РєСѓРїРєРё", "Р±СЋРґР¶РµС‚"]),
            [DocumentType.Act] = new(
                "РђРєС‚ РІС‹РїРѕР»РЅРµРЅРЅС‹С… СЂР°Р±РѕС‚ РёР»Рё СѓСЃР»СѓРі. РџСЂРѕРІРµСЂСЊС‚Рµ СЃС‚РѕСЂРѕРЅС‹, РґР°С‚Сѓ Рё СЃСѓРјРјСѓ.",
                ["incoming", "act", "closing"],
                ["Р°РєС‚", "act"],
                ["РІС‹РїРѕР»РЅРµРЅРЅС‹С… СЂР°Р±РѕС‚", "РѕРєР°Р·Р°РЅРЅС‹С… СѓСЃР»СѓРі", "РїСЂРёРµРјРєР°", "РїСЂРёРµРјРєРё"]),
            [DocumentType.Other] = new(
                "РќРµРґРѕСЃС‚Р°С‚РѕС‡РЅРѕ СѓРІРµСЂРµРЅРЅРѕРіРѕ СЃРёРіРЅР°Р»Р° РґР»СЏ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРѕР№ РєР»Р°СЃСЃРёС„РёРєР°С†РёРё, РїСЂРѕРІРµСЂСЊС‚Рµ С‚РёРї РІСЂСѓС‡РЅСѓСЋ.",
                ["incoming", "needs-review", "other"],
                ["С„Р°Р№Р»", "document", "scan"],
                ["СЃРєР°РЅ", "РІР»РѕР¶РµРЅРёРµ", "РґРѕРєСѓРјРµРЅС‚"])
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
            return BuildLowConfidenceResult("Р В РЎСљР В Р’ВµР В РўвЂР В РЎвЂўР РЋР С“Р РЋРІР‚С™Р В Р’В°Р РЋРІР‚С™Р В РЎвЂўР РЋРІР‚РЋР В Р вЂ¦Р В РЎвЂў Р В РўвЂР В Р’В°Р В Р вЂ¦Р В Р вЂ¦Р РЋРІР‚в„–Р РЋРІР‚В¦ Р В РўвЂР В Р’В»Р РЋР РЏ Р РЋРЎвЂњР В Р вЂ Р В Р’ВµР РЋР вЂљР В Р’ВµР В Р вЂ¦Р В Р вЂ¦Р В РЎвЂўР В РІвЂћвЂ“ Р В РЎвЂќР В Р’В»Р В Р’В°Р РЋР С“Р РЋР С“Р В РЎвЂР РЋРІР‚С›Р В РЎвЂР В РЎвЂќР В Р’В°Р РЋРІР‚В Р В РЎвЂР В РЎвЂ.");
        }

        var prediction = Predict(normalizedText);
        var predictedType = ResolvePredictedType(normalizedText, prediction.Score);
        var confidence = CalculateConfidence(prediction, normalizedText, predictedType);

        if (predictedType == DocumentType.Other || confidence < 0.60m)
        {
            return BuildLowConfidenceResult("Р В Р Р‹Р В РЎвЂР В РЎвЂ“Р В Р вЂ¦Р В Р’В°Р В Р’В»Р В РЎвЂўР В Р вЂ  Р В Р вЂ¦Р В Р’ВµР В РўвЂР В РЎвЂўР РЋР С“Р РЋРІР‚С™Р В Р’В°Р РЋРІР‚С™Р В РЎвЂўР РЋРІР‚РЋР В Р вЂ¦Р В РЎвЂў, Р РЋРІР‚С™Р В РЎвЂР В РЎвЂ” Р В Р’В»Р РЋРЎвЂњР РЋРІР‚РЋР РЋРІвЂљВ¬Р В Р’Вµ Р В РЎвЂ”Р В РЎвЂўР В РўвЂР РЋРІР‚С™Р В Р вЂ Р В Р’ВµР РЋР вЂљР В РўвЂР В РЎвЂР РЋРІР‚С™Р РЋР Р‰ Р В Р вЂ Р РЋР вЂљР РЋРЎвЂњР РЋРІР‚РЋР В Р вЂ¦Р РЋРЎвЂњР РЋР вЂ№.");
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
            ? $"Р В РІР‚СњР В РЎвЂўР В РЎвЂќР РЋРЎвЂњР В РЎВР В Р’ВµР В Р вЂ¦Р РЋРІР‚С™ #{document.DocumentId}"
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
                Label = "Р В РЎС›Р В РЎвЂР В РЎвЂ”",
                SuggestedValue = classification.SuggestedType.ToString(),
                ConfidenceScore = classification.ConfidenceScore
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "duedate",
                Label = "Р В Р Р‹Р РЋР вЂљР В РЎвЂўР В РЎвЂќ Р В РЎвЂР РЋР С“Р В РЎвЂ”Р В РЎвЂўР В Р’В»Р В Р вЂ¦Р В Р’ВµР В Р вЂ¦Р В РЎвЂР РЋР РЏ",
                SuggestedValue = dueDate,
                ConfidenceScore = Confidence(classification.ShouldAutoAssignType ? 0.84m : 0.67m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "title",
                Label = "Р В РЎСљР В Р’В°Р В Р’В·Р В Р вЂ Р В Р’В°Р В Р вЂ¦Р В РЎвЂР В Р’Вµ",
                SuggestedValue = title,
                ConfidenceScore = Confidence(0.88m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "description",
                Label = "Р В РЎвЂєР В РЎвЂ”Р В РЎвЂР РЋР С“Р В Р’В°Р В Р вЂ¦Р В РЎвЂР В Р’Вµ",
                SuggestedValue = description,
                ConfidenceScore = Confidence(string.IsNullOrWhiteSpace(document.ExtractedText) ? 0.63m : 0.86m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "priority",
                Label = "Р В РЎСџР РЋР вЂљР В РЎвЂР В РЎвЂўР РЋР вЂљР В РЎвЂР РЋРІР‚С™Р В Р’ВµР РЋРІР‚С™",
                SuggestedValue = priority,
                ConfidenceScore = Confidence(classification.SuggestedType == DocumentType.Invoice ? 0.79m : 0.68m)
            },
            new AiFieldSuggestionResult
            {
                FieldKey = "tags",
                Label = "Р В РЎС›Р В Р’ВµР В РЎвЂ“Р В РЎвЂ",
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
        var mlEvidence = mlPredictedType == DocumentType.Other ? 0.42m : CalculateEvidenceScore(normalizedText, mlPredictedType);

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

        if (bestType != DocumentType.Other && bestType != mlPredictedType && bestEvidence >= 0.84m && bestEvidence >= mlEvidence + 0.12m)
            return bestType;

        if (mlPredictedType != DocumentType.Other)
            return mlPredictedType;

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

            new() { Label = nameof(DocumentType.ServiceMemo), Text = "служебная записка инициатор подразделение тема пояснение" },
            new() { Label = nameof(DocumentType.ServiceMemo), Text = "memo internal note author department subject" },
            new() { Label = nameof(DocumentType.ServiceMemo), Text = "служебная записка о согласовании внутреннего вопроса" },
            new() { Label = nameof(DocumentType.ServiceMemo), Text = "пояснительная записка инициатор тема обращения" },
            new() { Label = nameof(DocumentType.ServiceMemo), Text = "internal memo department manager explanation" },

            new() { Label = nameof(DocumentType.PurchaseRequest), Text = "заявка на закупку предмет закупки плановая сумма обоснование" },
            new() { Label = nameof(DocumentType.PurchaseRequest), Text = "purchase request planned amount justification supplier need" },
            new() { Label = nameof(DocumentType.PurchaseRequest), Text = "заявка на согласование закупки количество бюджет" },
            new() { Label = nameof(DocumentType.PurchaseRequest), Text = "purchase request department item quantity finance" },
            new() { Label = nameof(DocumentType.PurchaseRequest), Text = "обоснование закупки товар услуга плановая сумма" },

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
