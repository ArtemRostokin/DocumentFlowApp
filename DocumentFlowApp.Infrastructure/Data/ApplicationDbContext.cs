using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFlowApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public ApplicationDbContext()
        {
        }
        // DbSet - представляет таблицу Documents в PostgreSQL
        public DbSet<Document> Documents { get; set; }

        /// Настройка подключения к PostgreSQL
        

        // Настройка моделей и отношений для PostgreSQL
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация для сущности Document
            modelBuilder.Entity<Document>(entity =>
            {
                // Указываем первичный ключ
                entity.HasKey(d => d.Id);

                // Настраиваем автоинкремент для PostgreSQL
                entity.Property(d => d.Id)
                    .ValueGeneratedOnAdd();

                // Название документа - обязательно
                entity.Property(d => d.Title)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasComment("Название документа"); // 💬 Комментарий в БД

                // Описание - не обязательно
                entity.Property(d => d.Description)
                    .HasMaxLength(1000)
                    .HasComment("Описание документа");

                // Дата создания - обязательно
                entity.Property(d => d.CreatedDate)
                    .IsRequired()
                    .HasDefaultValueSql("NOW()") // Значение по умолчанию в PostgreSQL
                    .HasComment("Дата создания документа");

                // Дата обновления
                entity.Property(d => d.UpdatedDate)
                    .HasComment("Дата последнего обновления");

                // Тип документа - сохраняем enum как строку
                entity.Property(d => d.Type)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasComment("Тип документа");

                // Статус документа - тоже сохраняем как строку
                entity.Property(d => d.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasComment("Статус документа");

                // Автор - обязательно
                entity.Property(d => d.Author)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasComment("Автор документа");

                // Путь к файлу
                entity.Property(d => d.FilePath)
                    .HasMaxLength(500)
                    .HasComment("Путь к файлу документа");

                // Создаем индекс для быстрого поиска по статусу
                entity.HasIndex(d => d.Status)
                    .HasDatabaseName("IX_Documents_Status");

                // Индекс для поиска по типу
                entity.HasIndex(d => d.Type)
                    .HasDatabaseName("IX_Documents_Type");

                // Индекс для сортировки по дате создания
                entity.HasIndex(d => d.CreatedDate)
                    .HasDatabaseName("IX_Documents_CreatedDate");
            });

            // Комментарий к таблице
            modelBuilder.Entity<Document>()
                .HasComment("Таблица для хранения документов системы");
        }
    }
}
