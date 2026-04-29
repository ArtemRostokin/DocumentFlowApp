using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentFlowApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalSpecializations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalSpecialization",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverSpecialization",
                table: "RouteSteps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverSpecialization",
                table: "DocumentApprovalSteps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalSpecialization",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApproverSpecialization",
                table: "RouteSteps");

            migrationBuilder.DropColumn(
                name: "ApproverSpecialization",
                table: "DocumentApprovalSteps");
        }
    }
}
