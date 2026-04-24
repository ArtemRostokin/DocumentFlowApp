using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Tests.TestDoubles;

internal sealed class FakeDocumentService : IDocumentService
{
    public List<Document> Documents { get; } = [];

    public Task<Document?> GetDocumentByIdAsync(int id)
    {
        return Task.FromResult(Documents.FirstOrDefault(x => x.DocumentId == id));
    }

    public Task<List<Document>> GetAllDocumentsAsync()
    {
        return Task.FromResult(Documents.ToList());
    }

    public Task<Document> CreateDocumentAsync(string title, string description, DocumentType type)
    {
        throw new NotSupportedException();
    }

    public Task<Document> CreateDocumentAsync(CreateDocumentRequest request)
    {
        throw new NotSupportedException();
    }

    public Task UpdateDocumentAsync(Document document)
    {
        throw new NotSupportedException();
    }

    public Task DeleteDocumentAsync(int id)
    {
        throw new NotSupportedException();
    }

    public Task ChangeDocumentStatusAsync(int documentId, DocumentStatus newStatus, string? transitionComment = null)
    {
        throw new NotSupportedException();
    }
}
