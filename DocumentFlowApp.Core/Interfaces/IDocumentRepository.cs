using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentFlowApp.Core.Interfaces
{
    // Репозиторий для работы с документами
    public interface IDocumentRepository
    {
        // Получить документ по ID
        Task<Document?> GetByIdAsync(int id);

        // Получить все документы
        Task<List<Document>> GetAllAsync();

        // Получить документы по статусу
        Task<List<Document>> GetByStatusAsync(DocumentStatus status);

        // Получить документы по типу
        Task<List<Document>> GetByTypeAsync(DocumentType type);
         
        // Добавить новый документ
        Task AddAsync(Document document);

        // Обновить документ
        Task UpdateAsync(Document document);

        // Удалить документ
        Task DeleteAsync(int id);

        // Сохранить изменения
        Task SaveChangesAsync();
    }
}
