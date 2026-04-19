using System;
using DocumentFlowApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(DatabaseConfig.GetConnectionString());
            }
        }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public ApplicationDbContext()
        {
        }

        // DbSet'ы для всех сущностей
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<AiModel> AiModels { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Template> Templates { get; set; } = null!;
        public DbSet<DocumentAiMetadata> DocumentAiMetadata { get; set; } = null!;
        public DbSet<DocumentRelation> DocumentRelations { get; set; } = null!;
        public DbSet<DocumentStatistic> DocumentStatistics { get; set; } = null!;
        public DbSet<DocumentActivity> DocumentActivity { get; set; } = null!;
        public DbSet<SearchHistory> SearchHistory { get; set; } = null!;
        public DbSet<UserSession> UserSessions { get; set; } = null!;

        // Настройка моделей и отношений для PostgreSQL
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация для сущности Document
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(d => d.DocumentId);

                entity.Property(d => d.DocumentId)
                    .ValueGeneratedOnAdd();

                entity.Property(d => d.Title)
                    .HasMaxLength(200)
                    .HasComment("Название документа");

                entity.Property(d => d.ExtractedText)
                    .HasComment("Извлечённый текст документа");

                entity.Property(d => d.CreatedDate)
                    .IsRequired()
                    .HasDefaultValueSql("NOW()")
                    .HasComment("Дата создания документа");

                entity.Property(d => d.UpdatedDate)
                    .HasComment("Дата последнего обновления");

                entity.Property(d => d.DocumentType)
                    .HasMaxLength(100)
                    .HasComment("Тип документа");

                entity.Property(d => d.Status)
                    .HasMaxLength(100)
                    .HasComment("Статус документа");

                entity.Property(d => d.FilePath)
                    .HasMaxLength(500)
                    .HasComment("Путь к файлу документа");

                // Индексы
                entity.HasIndex(d => d.Status).HasDatabaseName("IX_Documents_Status");
                entity.HasIndex(d => d.DocumentType).HasDatabaseName("IX_Documents_Type");
                entity.HasIndex(d => d.CreatedDate).HasDatabaseName("IX_Documents_CreatedDate");
            });

            // AiModel
            modelBuilder.Entity<AiModel>(entity =>
            {
                entity.HasKey(m => m.ModelId);
                entity.Property(m => m.ModelId).ValueGeneratedOnAdd();
                entity.Property(m => m.ModelName).IsRequired().HasMaxLength(200);
                entity.Property(m => m.ModelData).HasColumnType("jsonb").HasComment("JSON с данными модели");
            });

            // Template
            modelBuilder.Entity<Template>(entity =>
            {
                entity.HasKey(t => t.TemplateId);
                entity.Property(t => t.TemplateId).ValueGeneratedOnAdd();
                entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
                entity.Property(t => t.AiSuggestedFields).HasColumnType("jsonb").HasComment("JSON предложенных полей AI");
            });

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.UserId).ValueGeneratedOnAdd();
                entity.Property(u => u.UserName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
                entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");
            });

            // Role, Permission, RolePermission (составной ключ)
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleId);
                entity.Property(r => r.RoleId).ValueGeneratedOnAdd();
                entity.Property(r => r.RoleName).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(p => p.PermissionId);
                entity.Property(p => p.PermissionId).ValueGeneratedOnAdd();
                entity.Property(p => p.PermissionName).IsRequired().HasMaxLength(150);
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

                entity.HasOne(rp => rp.Role)
                      .WithMany(r => r.RolePermissions)
                      .HasForeignKey(rp => rp.RoleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.Permission)
                      .WithMany(p => p.RolePermissions)
                      .HasForeignKey(rp => rp.PermissionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // DocumentAiMetadata
            modelBuilder.Entity<DocumentAiMetadata>(entity =>
            {
                entity.HasKey(m => m.MetadataId);
                entity.Property(m => m.MetadataId).ValueGeneratedOnAdd();
                entity.Property(m => m.ExtractedEntities).HasColumnType("jsonb");
                entity.HasOne(m => m.Document)
                      .WithMany(d => d.AiMetadata)
                      .HasForeignKey(m => m.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Model)
                      .WithMany()
                      .HasForeignKey(m => m.ModelId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // DocumentRelation (source/target)
            modelBuilder.Entity<DocumentRelation>(entity =>
            {
                entity.HasKey(r => r.RelationId);
                entity.HasOne(r => r.SourceDocument)
                      .WithMany(d => d.SourceRelations)
                      .HasForeignKey(r => r.SourceDocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.TargetDocument)
                      .WithMany(d => d.TargetRelations)
                      .HasForeignKey(r => r.TargetDocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // DocumentStatistic
            modelBuilder.Entity<DocumentStatistic>(entity =>
            {
                entity.HasKey(s => s.StatId);
                entity.HasOne(s => s.Document)
                      .WithMany(d => d.Statistics)
                      .HasForeignKey(s => s.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.User)
                      .WithMany(u => u.DocumentStatistics)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // DocumentActivity
            modelBuilder.Entity<DocumentActivity>(entity =>
            {
                entity.HasKey(a => a.ActivityId);
                entity.HasOne(a => a.Document)
                      .WithMany(d => d.Activities)
                      .HasForeignKey(a => a.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.User)
                      .WithMany(u => u.DocumentActivities)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // SearchHistory
            modelBuilder.Entity<SearchHistory>(entity =>
            {
                entity.HasKey(s => s.SearchId);
                entity.Property(s => s.SearchFilters).HasColumnType("jsonb");
                entity.HasOne(s => s.User)
                      .WithMany(u => u.SearchHistories)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // UserSession
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(s => s.SessionId);
                entity.Property(s => s.Token).HasMaxLength(2000);
                entity.HasOne(s => s.User)
                      .WithMany(u => u.Sessions)
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Общие комментарии к таблицам (опционально)
            modelBuilder.Entity<Document>().HasComment("Таблица для хранения документов системы");
            modelBuilder.Entity<AiModel>().HasComment("AI модели");
            modelBuilder.Entity<Template>().HasComment("Шаблоны документов");
            modelBuilder.Entity<User>().HasComment("Пользователи системы");
        }
    }
}
