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
                .FirstOrDefaultAsync(d => d.DocumentId == id);
        }

        public async Task<List<Document>> GetAllAsync()
        {
            return await _context.Documents
                .Include(d => d.User)
                .OrderByDescending(d => d.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Document>> GetByStatusAsync(DocumentStatus status)
        {
            var statusString = status.ToString();
            return await _context.Documents
                .Where(d => d.Status == statusString)
                .OrderByDescending(d => d.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Document>> GetByTypeAsync(DocumentType type)
        {
            var typeString = type.ToString();
            return await _context.Documents
                .Where(d => d.DocumentType == typeString)
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
