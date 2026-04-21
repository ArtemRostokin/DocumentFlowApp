using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Services;

namespace DocumentFlowApp.Tests;

public class DocumentServiceStatusTests
{
    [Fact]
    public async Task Allows_LinearStatusFlow()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Draft.ToString());
        var service = new DocumentService(repository);

        await service.ChangeDocumentStatusAsync(1, DocumentStatus.OnApproval);
        await service.ChangeDocumentStatusAsync(1, DocumentStatus.Approved);
        await service.ChangeDocumentStatusAsync(1, DocumentStatus.InWork);
        await service.ChangeDocumentStatusAsync(1, DocumentStatus.Completed);
        await service.ChangeDocumentStatusAsync(1, DocumentStatus.Archived);

        Assert.Equal(DocumentStatus.Archived.ToString(), repository.StoredDocument!.Status);
    }

    [Fact]
    public async Task Rejects_SkippedStageTransition()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Draft.ToString());
        var service = new DocumentService(repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeDocumentStatusAsync(1, DocumentStatus.Approved));

        Assert.Contains("Недопустимый переход статуса", error.Message);
    }

    [Fact]
    public async Task Requires_Comment_WhenReturningToDraft()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.OnApproval.ToString());
        var service = new DocumentService(repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeDocumentStatusAsync(1, DocumentStatus.Draft));

        Assert.Contains("Комментарий обязателен", error.Message);
    }

    [Fact]
    public async Task Allows_ReturnToDraft_WithComment()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.OnApproval.ToString());
        var service = new DocumentService(repository);

        await service.ChangeDocumentStatusAsync(1, DocumentStatus.Draft, "Needs corrections.");

        Assert.Equal(DocumentStatus.Draft.ToString(), repository.StoredDocument!.Status);
    }

    [Fact]
    public async Task Blocks_Changes_FromArchived()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Archived.ToString());
        var service = new DocumentService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeDocumentStatusAsync(1, DocumentStatus.Draft, "Reopen"));
    }

    [Fact]
    public async Task Supports_LegacyInProgress_StatusMapping()
    {
        var repository = CreateRepositoryWithStatus("InProgress");
        var service = new DocumentService(repository);

        await service.ChangeDocumentStatusAsync(1, DocumentStatus.Approved);

        Assert.Equal(DocumentStatus.Approved.ToString(), repository.StoredDocument!.Status);
    }

    private static InMemoryDocumentRepository CreateRepositoryWithStatus(string status)
    {
        return new InMemoryDocumentRepository(
            new Document
            {
                DocumentId = 1,
                Title = "Sample",
                Status = status,
                DocumentType = DocumentType.Other.ToString(),
                CreatedDate = DateTime.UtcNow
            });
    }

    private sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        public Document? StoredDocument { get; private set; }

        public InMemoryDocumentRepository(Document document)
        {
            StoredDocument = document;
        }

        public Task<Document?> GetByIdAsync(int id)
        {
            return Task.FromResult(StoredDocument?.DocumentId == id ? StoredDocument : null);
        }

        public Task<List<Document>> GetAllAsync()
        {
            return Task.FromResult(StoredDocument is null ? new List<Document>() : new List<Document> { StoredDocument });
        }

        public Task<List<Document>> GetByStatusAsync(DocumentStatus status)
        {
            if (StoredDocument is not null && string.Equals(StoredDocument.Status, status.ToString(), StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new List<Document> { StoredDocument });

            return Task.FromResult(new List<Document>());
        }

        public Task<List<Document>> GetByTypeAsync(DocumentType type)
        {
            if (StoredDocument is not null && string.Equals(StoredDocument.DocumentType, type.ToString(), StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new List<Document> { StoredDocument });

            return Task.FromResult(new List<Document>());
        }

        public Task AddAsync(Document document)
        {
            StoredDocument = document;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Document document)
        {
            StoredDocument = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id)
        {
            if (StoredDocument?.DocumentId == id)
                StoredDocument = null;

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync()
        {
            return Task.CompletedTask;
        }
    }
}
