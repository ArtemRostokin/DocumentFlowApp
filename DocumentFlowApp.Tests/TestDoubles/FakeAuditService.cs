using DocumentFlowApp.Core.Interfaces;

namespace DocumentFlowApp.Tests.TestDoubles;

internal sealed class FakeAuditService : IAuditService
{
    public List<(int? DocumentId, int? UserId, string ActivityType, string Details)> Entries { get; } = [];

    public Task LogDocumentActivityAsync(int documentId, int? userId, string activityType, string details, CancellationToken cancellationToken = default)
    {
        Entries.Add((documentId, userId, activityType, details));
        return Task.CompletedTask;
    }

    public Task LogSystemActivityAsync(int? userId, string activityType, string details, CancellationToken cancellationToken = default)
    {
        Entries.Add((null, userId, activityType, details));
        return Task.CompletedTask;
    }
}
