using DocumentFlowApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocumentFlowApp.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260428130000_AddApprovalRoutes")]
    public partial class AddApprovalRoutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RouteTemplateId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RouteTemplates",
                columns: table => new
                {
                    RouteTemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteTemplates", x => x.RouteTemplateId);
                },
                comment: "Шаблоны маршрутов согласования");

            migrationBuilder.CreateTable(
                name: "RouteSteps",
                columns: table => new
                {
                    RouteStepId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RouteTemplateId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApproverUserId = table.Column<int>(type: "integer", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteSteps", x => x.RouteStepId);
                    table.ForeignKey(
                        name: "FK_RouteSteps_RouteTemplates_RouteTemplateId",
                        column: x => x.RouteTemplateId,
                        principalTable: "RouteTemplates",
                        principalColumn: "RouteTemplateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteSteps_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Шаги шаблона маршрута согласования");

            migrationBuilder.CreateTable(
                name: "DocumentApprovalSteps",
                columns: table => new
                {
                    DocumentApprovalStepId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    RouteTemplateId = table.Column<int>(type: "integer", nullable: true),
                    RouteStepId = table.Column<int>(type: "integer", nullable: true),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApproverUserId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActionByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentApprovalSteps", x => x.DocumentApprovalStepId);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalSteps_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalSteps_RouteSteps_RouteStepId",
                        column: x => x.RouteStepId,
                        principalTable: "RouteSteps",
                        principalColumn: "RouteStepId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalSteps_RouteTemplates_RouteTemplateId",
                        column: x => x.RouteTemplateId,
                        principalTable: "RouteTemplates",
                        principalColumn: "RouteTemplateId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalSteps_Users_ActionByUserId",
                        column: x => x.ActionByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalSteps_Users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Шаги согласования, привязанные к конкретному документу");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RouteTemplateId",
                table: "Documents",
                column: "RouteTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalSteps_ActionByUserId",
                table: "DocumentApprovalSteps",
                column: "ActionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalSteps_ApproverUserId",
                table: "DocumentApprovalSteps",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalSteps_DocumentId_IsCurrent",
                table: "DocumentApprovalSteps",
                columns: new[] { "DocumentId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalSteps_DocumentId_StepOrder",
                table: "DocumentApprovalSteps",
                columns: new[] { "DocumentId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalSteps_RouteStepId",
                table: "DocumentApprovalSteps",
                column: "RouteStepId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalSteps_RouteTemplateId",
                table: "DocumentApprovalSteps",
                column: "RouteTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteSteps_ApproverUserId",
                table: "RouteSteps",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteSteps_RouteTemplateId_StepOrder",
                table: "RouteSteps",
                columns: new[] { "RouteTemplateId", "StepOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_RouteTemplates_RouteTemplateId",
                table: "Documents",
                column: "RouteTemplateId",
                principalTable: "RouteTemplates",
                principalColumn: "RouteTemplateId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_RouteTemplates_RouteTemplateId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "DocumentApprovalSteps");

            migrationBuilder.DropTable(
                name: "RouteSteps");

            migrationBuilder.DropTable(
                name: "RouteTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Documents_RouteTemplateId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RouteTemplateId",
                table: "Documents");
        }
    }
}
