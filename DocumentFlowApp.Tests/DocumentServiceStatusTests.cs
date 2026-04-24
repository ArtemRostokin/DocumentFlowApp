using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Models;
using DocumentFlowApp.Services;
using DocumentFlowApp.Tests.TestDoubles;

namespace DocumentFlowApp.Tests;

public class DocumentServiceStatusTests
{
    [Fact]
    public async Task Allows_LinearStatusFlow()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Draft.ToString(), nomenclatureCaseId: 10);
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

    [Fact]
    public async Task Blocks_Archive_WithoutNomenclature()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Completed.ToString());
        var service = new DocumentService(repository);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeDocumentStatusAsync(1, DocumentStatus.Archived));

        Assert.Contains("делу номенклатуры", error.Message);
    }

    [Fact]
    public async Task Allows_Archive_WithNomenclature()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Completed.ToString(), nomenclatureCaseId: 42);
        var service = new DocumentService(repository);

        await service.ChangeDocumentStatusAsync(1, DocumentStatus.Archived);

        Assert.Equal(DocumentStatus.Archived.ToString(), repository.StoredDocument!.Status);
    }

    [Fact]
    public async Task CreateDocumentAsync_Stores_Template_And_File_Metadata()
    {
        var repository = new InMemoryDocumentRepository();
        var service = new DocumentService(repository);
        var dueDate = new DateTime(2026, 4, 24, 12, 30, 00, DateTimeKind.Unspecified);

        var document = await service.CreateDocumentAsync(new CreateDocumentRequest
        {
            Title = "Contract",
            Description = "Body",
            Type = DocumentType.Contract,
            TemplateId = 7,
            DueDate = dueDate,
            Priority = 3,
            Tags = "important",
            FilePath = "contract.pdf",
            FileSize = 1024,
            FileHash = "abc123"
        });

        Assert.Equal("Contract", document.Title);
        Assert.Equal(DocumentType.Contract.ToString(), document.DocumentType);
        Assert.Equal(DocumentStatus.Draft.ToString(), document.Status);
        Assert.Equal(7, document.TemplateId);
        Assert.Equal(3, document.Priority);
        Assert.Equal("important", document.Tags);
        Assert.Equal("contract.pdf", document.FilePath);
        Assert.Equal(1024, document.FileSize);
        Assert.Equal("abc123", document.FileHash);
        Assert.Equal(DateTimeKind.Utc, document.DueDate!.Value.Kind);
        Assert.Same(document, repository.StoredDocument);
    }

    [Fact]
    public async Task CreateDocumentAsync_Rejects_EmptyTitle()
    {
        var repository = new InMemoryDocumentRepository();
        var service = new DocumentService(repository);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateDocumentAsync(new CreateDocumentRequest
            {
                Title = "",
                Description = "Body",
                Type = DocumentType.Other
            }));

        Assert.Contains("Название документа не может быть пустым", error.Message);
    }

    [Fact]
    public async Task CreateDocumentAsync_Rejects_TooLongTitle()
    {
        var repository = new InMemoryDocumentRepository();
        var service = new DocumentService(repository);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateDocumentAsync(new CreateDocumentRequest
            {
                Title = new string('A', 201),
                Description = "Body",
                Type = DocumentType.Other
            }));

        Assert.Contains("Название документа слишком длинное", error.Message);
    }

    [Fact]
    public async Task UpdateDocumentAsync_Sets_UpdatedDate()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Draft.ToString());
        var service = new DocumentService(repository);
        var document = repository.StoredDocument!;

        Assert.Null(document.UpdatedDate);

        await service.UpdateDocumentAsync(document);

        Assert.NotNull(document.UpdatedDate);
        Assert.True(document.UpdatedDate <= DateTime.UtcNow);
    }

    [Fact]
    public async Task DeleteDocumentAsync_Removes_Document()
    {
        var repository = CreateRepositoryWithStatus(DocumentStatus.Draft.ToString());
        var service = new DocumentService(repository);

        await service.DeleteDocumentAsync(1);

        Assert.Null(repository.StoredDocument);
    }

    private static InMemoryDocumentRepository CreateRepositoryWithStatus(string status, int? nomenclatureCaseId = null)
    {
        return new InMemoryDocumentRepository(
            new Document
            {
                DocumentId = 1,
                Title = "Sample",
                Status = status,
                DocumentType = DocumentType.Other.ToString(),
                NomenclatureCaseId = nomenclatureCaseId,
                CreatedDate = DateTime.UtcNow
            });
    }
}
