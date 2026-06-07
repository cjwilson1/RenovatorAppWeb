using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeLookup : Migration
    {
        private static readonly Guid InspectionDocumentTypeId = Guid.Parse("2f5c7a2d-89a5-4f97-9b6f-70c9f47a1f01");
        private static readonly Guid CustomerDataSheetDocumentTypeId = Guid.Parse("9a9f93a7-9b6e-45c9-bc19-cc6cb17635b5");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentType",
                columns: table => new
                {
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentType", x => x.DocumentTypeId);
                });

            migrationBuilder.InsertData(
                table: "DocumentType",
                columns: new[] { "DocumentTypeId", "Name" },
                values: new object[,]
                {
                    { InspectionDocumentTypeId, "Inspection" },
                    { CustomerDataSheetDocumentTypeId, "Customer DataSheet" }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentTypeId",
                table: "Documents",
                type: "uuid",
                nullable: false,
                defaultValue: CustomerDataSheetDocumentTypeId);

            migrationBuilder.Sql($"""
                UPDATE "Documents"
                SET "DocumentTypeId" = '{InspectionDocumentTypeId}'
                WHERE lower("DocumentType") = 'inspection';
                """);

            migrationBuilder.DropIndex(
                name: "IX_Documents_DocumentType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentType_Name",
                table: "DocumentType",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentType_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId",
                principalTable: "DocumentType",
                principalColumn: "DocumentTypeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentType_DocumentTypeId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents");

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql($"""
                UPDATE "Documents"
                SET "DocumentType" = CASE
                    WHEN "DocumentTypeId" = '{InspectionDocumentTypeId}' THEN 'inspection'
                    WHEN "DocumentTypeId" = '{CustomerDataSheetDocumentTypeId}' THEN 'customer'
                    ELSE ''
                END;
                """);

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentType",
                table: "Documents",
                column: "DocumentType");

            migrationBuilder.DropTable(
                name: "DocumentType");
        }
    }
}
