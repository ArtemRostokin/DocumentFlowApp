using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocumentFlowApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Название документа"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, comment: "Описание документа"),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Тип документа"),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Статус документа"),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()", comment: "Дата создания документа"),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата последнего обновления"),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Путь к файлу документа"),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Автор документа")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                },
                comment: "Таблица для хранения документов системы");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedDate",
                table: "Documents",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status",
                table: "Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Type",
                table: "Documents",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");
        }
    }
}
