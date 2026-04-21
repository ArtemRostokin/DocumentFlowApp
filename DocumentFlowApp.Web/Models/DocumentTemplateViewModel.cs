namespace DocumentFlowApp.Web.Models;

public sealed class DocumentTemplateViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public IReadOnlyList<DocumentTemplateFieldViewModel> Fields { get; init; } = [];
}

public sealed class DocumentTemplateFieldViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Placeholder { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string InputType { get; init; } = "text";
}