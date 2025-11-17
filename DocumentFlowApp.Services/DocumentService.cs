using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;

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
            //Бизнес-логика: валидация и создание документа
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название документа не может быть пустым");

            if (title.Length > 200)
                throw new ArgumentException("Название документа слишком длинное");

            var document = new Document
            {
                Title = title.Trim(),
                Description = description?.Trim() ?? string.Empty,
                Type = type,
                Status = DocumentStatus.Draft,
                CreatedDate = DateTime.UtcNow,
                Author = "System" //Позже заменить на реального пользователя
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

            //Бизнес-логика: проверка допустимых переходов статусов
            if (document.Status == DocumentStatus.Archived && newStatus != DocumentStatus.Archived)
                throw new InvalidOperationException("Нельзя изменить статус архивного документа");

            document.Status = newStatus;
            document.UpdatedDate = DateTime.UtcNow;

            await _documentRepository.UpdateAsync(document);
        }
    }
}
