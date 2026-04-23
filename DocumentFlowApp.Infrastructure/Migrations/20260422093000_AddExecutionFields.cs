using System;
using DocumentFlowApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentFlowApp.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260422093000_AddExecutionFields")]
    public partial class AddExecutionFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionComment",
                table: "Documents",
                type: "text",
                nullable: true,
                comment: "Комментарий исполнителя по ходу работы");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutionCompletedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата завершения исполнения документа");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionFileName",
                table: "Documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "Имя итогового файла исполнения");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionFilePath",
                table: "Documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Путь к итоговому файлу исполнения");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionResult",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "Результат исполнения документа");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutionStartedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата начала исполнения документа");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionComment",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutionCompletedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutionFileName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutionFilePath",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutionResult",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutionStartedAt",
                table: "Documents");
        }
    }
}
