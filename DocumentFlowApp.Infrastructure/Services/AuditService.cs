using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace DocumentFlowApp.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext dbContext, ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogDocumentActivityAsync(
        int documentId,
        int? userId,
        string activityType,
        string details,
        CancellationToken cancellationToken = default)
    {
        await LogActivityCoreAsync(documentId, userId, activityType, details, cancellationToken);
    }

    public async Task LogSystemActivityAsync(
        int? userId,
        string activityType,
        string details,
        CancellationToken cancellationToken = default)
    {
        await LogActivityCoreAsync(null, userId, activityType, details, cancellationToken);
    }

    private async Task LogActivityCoreAsync(
        int? documentId,
        int? userId,
        string activityType,
        string details,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.DocumentActivity.Add(new DocumentActivity
            {
                DocumentId = documentId,
                UserId = userId,
                ActivityType = activityType,
                ActivityDate = DateTime.UtcNow,
                Details = details
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось записать аудит по документу {DocumentId} ({ActivityType})",
                documentId,
                activityType);
        }
    }
}
