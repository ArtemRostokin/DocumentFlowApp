using DocumentFlowApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentFlowApp.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260424113000_AllowSystemAuditEvents")]
    public partial class AllowSystemAuditEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentActivity_Documents_DocumentId",
                table: "DocumentActivity");

            migrationBuilder.AlterColumn<int>(
                name: "DocumentId",
                table: "DocumentActivity",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentActivity_Documents_DocumentId",
                table: "DocumentActivity",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "DocumentId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentActivity_Documents_DocumentId",
                table: "DocumentActivity");

            migrationBuilder.Sql(
                """
                DELETE FROM "DocumentActivity"
                WHERE "DocumentId" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "DocumentId",
                table: "DocumentActivity",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentActivity_Documents_DocumentId",
                table: "DocumentActivity",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "DocumentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
