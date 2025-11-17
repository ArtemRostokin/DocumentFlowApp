using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Enums;
using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentFlowApp.Infrastructure.Repositories
{
    // Реализация репозитория для работы с документами
    public class DocumentRepository : IDocumentRepository
    {
        private readonly ApplicationDbContext _context;

        public DocumentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Document?> GetByIdAsync(int id)
        {
            return await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Document>> GetAllAsync()
        {
            return await _context.Documents
                .OrderByDescending(d => d.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Document>> GetByStatusAsync(DocumentStatus status)
        {
            return await _context.Documents
                .Where(d => d.Status == status)
                .OrderByDescending(d => d.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Document>> GetByTypeAsync(DocumentType type)
        {
            return await _context.Documents
                .Where(d => d.Type == type)
                .OrderByDescending(d => d.CreatedDate)
                .ToListAsync();
        }

        public async Task AddAsync(Document document)
        {
            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Document document)
        {
            _context.Documents.Update(document);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var document = await GetByIdAsync(id);
            if (document != null)
            {
                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
