namespace DocumentFlowApp.Web.Models;

public sealed class UploadIncomingDocumentsPageViewModel
{
    public List<IFormFile> Files { get; set; } = [];
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalFiles { get; set; }
    public int ImportedCount { get; set; }
    public int AutoClassifiedCount { get; set; }
    public int NeedsReviewCount { get; set; }
    public int LowConfidenceCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<UploadIncomingFileResultViewModel> Results { get; set; } = [];
}

public sealed class UploadIncomingFileResultViewModel
{
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string BadgeClass { get; init; } = string.Empty;
    public int? DocumentId { get; init; }
    public string? DocumentTitle { get; init; }
    public string? TypeLabel { get; init; }
    public int? ConfidencePercent { get; init; }
}
