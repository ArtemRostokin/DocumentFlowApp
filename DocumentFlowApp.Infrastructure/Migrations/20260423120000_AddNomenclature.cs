using DocumentFlowApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocumentFlowApp.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260423120000_AddNomenclature")]
    public partial class AddNomenclature : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NomenclatureCases",
                columns: table => new
                {
                    NomenclatureCaseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Index = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RetentionPeriod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Department = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NomenclatureCases", x => x.NomenclatureCaseId);
                },
                comment: "Дела номенклатуры");

            migrationBuilder.CreateTable(
                name: "NomenclatureRules",
                columns: table => new
                {
                    NomenclatureRuleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomenclatureCaseId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NomenclatureRules", x => x.NomenclatureRuleId);
                    table.ForeignKey(
                        name: "FK_NomenclatureRules_NomenclatureCases_NomenclatureCaseId",
                        column: x => x.NomenclatureCaseId,
                        principalTable: "NomenclatureCases",
                        principalColumn: "NomenclatureCaseId",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Правила автопривязки дел номенклатуры");

            migrationBuilder.AddColumn<int>(
                name: "NomenclatureCaseId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_NomenclatureCaseId",
                table: "Documents",
                column: "NomenclatureCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_NomenclatureCases_Index",
                table: "NomenclatureCases",
                column: "Index",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NomenclatureRules_NomenclatureCaseId",
                table: "NomenclatureRules",
                column: "NomenclatureCaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_NomenclatureCases_NomenclatureCaseId",
                table: "Documents",
                column: "NomenclatureCaseId",
                principalTable: "NomenclatureCases",
                principalColumn: "NomenclatureCaseId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_NomenclatureCases_NomenclatureCaseId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "NomenclatureRules");

            migrationBuilder.DropTable(
                name: "NomenclatureCases");

            migrationBuilder.DropIndex(
                name: "IX_Documents_NomenclatureCaseId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NomenclatureCaseId",
                table: "Documents");
        }
    }
}
