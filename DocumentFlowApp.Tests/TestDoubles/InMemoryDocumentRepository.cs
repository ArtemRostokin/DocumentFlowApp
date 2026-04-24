using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;

namespace DocumentFlowApp.Tests.TestDoubles;

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    public Document? StoredDocument { get; private set; }

    public InMemoryDocumentRepository(Document? document = null)
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
