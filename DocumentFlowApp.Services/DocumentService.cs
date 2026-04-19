using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Core.Models;

namespace DocumentFlowApp.Services
{
    // Реализация сервиса для работы с документами
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;

        public DocumentService(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository;
        }

        private static DateTime? NormalizeToUtc(DateTime? value)
        {
            if (value is null)
                return null;

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                // Для timestamptz Npgsql требует UTC, поэтому явно помечаем как UTC.
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        public async Task<Document?> GetDocumentByIdAsync(int id)
        {
            return await _documentRepository.GetByIdAsync(id);
        }

        public async Task<List<Document>> GetAllDocumentsAsync()
        {
            return await _documentRepository.GetAllAsync();
        }

        public async Task<Document> CreateDocumentAsync(string title, string description, DocumentType type)
        {
            return await CreateDocumentAsync(new CreateDocumentRequest
            {
                Title = title,
                Description = description,
                Type = type
            });
        }

        public async Task<Document> CreateDocumentAsync(CreateDocumentRequest request)
        {
            // Бизнес-логика: валидация и создание документа
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Название документа не может быть пустым");

            if (request.Title.Length > 200)
                throw new ArgumentException("Название документа слишком длинное");

            var document = new Document
            {
                Title = request.Title,
                ExtractedText = request.Description,
                DocumentType = request.Type.ToString(),
                Status = DocumentStatus.Draft.ToString(),
                CreatedDate = DateTime.UtcNow,
                IsArchived = false,
                DueDate = NormalizeToUtc(request.DueDate),
                Priority = request.Priority,
                Tags = request.Tags,
                FilePath = request.FilePath,
                FileSize = request.FileSize,
                FileHash = request.FileHash
            };

            await _documentRepository.AddAsync(document);
            return document;
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            document.UpdatedDate = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document);
        }

        public async Task DeleteDocumentAsync(int id)
        {
            await _documentRepository.DeleteAsync(id);
        }

        public async Task ChangeDocumentStatusAsync(int documentId, DocumentStatus newStatus)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new ArgumentException("Документ не найден");

            var currentStatus = ParseDocumentStatus(document.Status);

            // Бизнес-логика: проверка допустимых переходов статусов (без пропусков этапов)
            // Правила:
            // - Draft -> InProgress
            // - InProgress -> Approved
            // - Approved -> Archived
            // - Любой (кроме Archived) -> Rejected
            // - Rejected -> InProgress
            // - Из Archived нельзя выходить
            if (!IsValidTransition(currentStatus, newStatus))
                throw new InvalidOperationException($"Недопустимый переход статуса: {currentStatus} → {newStatus}");

            document.Status = newStatus.ToString();
            document.UpdatedDate = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(document);
        }

        private static DocumentStatus ParseDocumentStatus(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return DocumentStatus.Draft;

            if (Enum.TryParse<DocumentStatus>(stored, true, out var parsed))
                return parsed;

            if (int.TryParse(stored, out var intVal) && Enum.IsDefined(typeof(DocumentStatus), intVal))
                return (DocumentStatus)intVal;

            return DocumentStatus.Draft;
        }

        private static bool IsValidTransition(DocumentStatus from, DocumentStatus to)
        {
            if (from == to)
                return true;

            if (from == DocumentStatus.Archived)
                return false;

            if (to == DocumentStatus.Rejected)
                return true;

            return from switch
            {
                DocumentStatus.Draft => to == DocumentStatus.InProgress,
                DocumentStatus.InProgress => to == DocumentStatus.Approved,
                DocumentStatus.Approved => to == DocumentStatus.Archived,
                DocumentStatus.Rejected => to == DocumentStatus.InProgress,
                _ => false
            };
        }
    }
}
