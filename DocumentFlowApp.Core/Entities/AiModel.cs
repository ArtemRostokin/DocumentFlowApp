using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DocumentFlowApp.Core.Entities;

public class AiModel
{
    [Key]
    public int ModelId { get; set; }

    public string ModelName { get; set; } = null!;
    public string? ModelType { get; set; }
    public string? Version { get; set; }
    public bool IsActive { get; set; }
    public decimal? Accuracy { get; set; }
    public DateTime? LastTrained { get; set; }

    // raw JSON stored as string; можно заменить на JsonDocument/JsonNode с конвертером EF
    public string? ModelData { get; set; }
}