using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentFlowApp.Core.Interfaces
{
    // Сервис для бизнес-логики работы с документами
    public interface IDocumentService
    {
        // Получить документ по ID
        Task<Document?> GetDocumentByIdAsync(int id);

        // Получить все документы
        Task<List<Document>> GetAllDocumentsAsync();

        // Создать новый документ
        Task<Document> CreateDocumentAsync(string title, string description, DocumentType type);
        Task<Document> CreateDocumentAsync(CreateDocumentRequest request);

        // Обновить документ
        Task UpdateDocumentAsync(Document document);

        // Удалить документ
        Task DeleteDocumentAsync(int id);

        // Изменить статус документа
        Task ChangeDocumentStatusAsync(int documentId, DocumentStatus newStatus, string? transitionComment = null);
    }
}
