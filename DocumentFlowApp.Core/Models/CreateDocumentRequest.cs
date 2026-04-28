using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Core.Models;

public class CreateDocumentRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DocumentType Type { get; init; }
    public int? TemplateId { get; init; }
    public int? RouteTemplateId { get; init; }
    public DateTime? DueDate { get; init; }
    public int? Priority { get; init; }
    public string? Tags { get; init; }
    public string? FilePath { get; init; }
    public long? FileSize { get; init; }
    public string? FileHash { get; init; }
}
