using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFlowApp.Core.Enums;

namespace DocumentFlowApp.Core.Entities
{
    // Основная сущность - Документ, представляет любой документ в системе
    public class Document
    {
        public int Id { get; set; }                                  // Первичный ключ - уникальный идентификатор
        public string Title { get; set; } = string.Empty;            // Название документа 
        public string Description { get; set; } = string.Empty;      // Описание/содержание документа
        public DocumentType Type { get; set; }                       // Тип документа 
        public DocumentStatus Status { get; set; }                   // Текущий статус 
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Дата создания 
        public DateTime? UpdatedDate { get; set; }                   // Дата последнего изменения 
        public string FilePath { get; set; } = string.Empty;         // Путь к файлу документа на диске
        public string Author { get; set; } = "System";               // Автор документа (потом обновить)
    }
}
