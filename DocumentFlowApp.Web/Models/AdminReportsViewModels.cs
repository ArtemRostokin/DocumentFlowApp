using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Web.Models;

public sealed class AdminReportPageViewModel
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public DocumentType? SelectedType { get; init; }
    public string? SelectedStatus { get; init; }
    public int TotalDocuments { get; init; }
    public int CompletedDocuments { get; init; }
    public int PendingDocuments { get; init; }
    public int OverdueDocuments { get; init; }
    public double AverageCycleDays { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public IReadOnlyList<AdminReportBreakdownItemViewModel> StatusBreakdown { get; init; } = [];
    public IReadOnlyList<AdminReportBreakdownItemViewModel> TypeBreakdown { get; init; } = [];
    public IReadOnlyList<AdminReportTrendPointViewModel> DailyTrend { get; init; } = [];
    public IReadOnlyList<AdminReportCycleByTypeItemViewModel> AverageCycleByType { get; init; } = [];
    public IReadOnlyList<AdminReportBreakdownItemViewModel> OverdueByType { get; init; } = [];
    public IReadOnlyList<AdminReportBreakdownItemViewModel> RoleBreakdown { get; init; } = [];
    public IReadOnlyList<AdminReportDocumentRowViewModel> OverdueItems { get; init; } = [];
    public IReadOnlyList<AdminReportDocumentRowViewModel> RecentItems { get; init; } = [];
}

public sealed class AdminReportBreakdownItemViewModel
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public double SharePercent { get; init; }
}

public sealed class AdminReportDocumentRowViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = string.Empty;
    public DateTime CreatedDateUtc { get; init; }
    public DateTime? DueDateUtc { get; init; }
}

public sealed class AdminReportTrendPointViewModel
{
    public DateTime Date { get; init; }
    public string ShortLabel { get; init; } = string.Empty;
    public int CreatedCount { get; init; }
    public int CompletedCount { get; init; }
}

public sealed class AdminReportCycleByTypeItemViewModel
{
    public string Label { get; init; } = string.Empty;
    public int CompletedCount { get; init; }
    public double AverageDays { get; init; }
    public int OverdueCount { get; init; }
}
