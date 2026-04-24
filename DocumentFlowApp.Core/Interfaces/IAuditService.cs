namespace DocumentFlowApp.Core.Interfaces;

public interface IAuditService
{
    Task LogDocumentActivityAsync(
        int documentId,
        int? userId,
        string activityType,
        string details,
        CancellationToken cancellationToken = default);
}
