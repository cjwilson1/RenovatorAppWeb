using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentInspectionAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InspectionId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_InspectionId",
                table: "Documents",
                column: "InspectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Inspection_InspectionId",
                table: "Documents",
                column: "InspectionId",
                principalTable: "Inspection",
                principalColumn: "InspectionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Inspection_InspectionId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_InspectionId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InspectionId",
                table: "Documents");
        }
    }
}
