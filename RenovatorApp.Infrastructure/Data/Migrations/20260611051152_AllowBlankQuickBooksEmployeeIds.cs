using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowBlankQuickBooksEmployeeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employee_RenoCompanyID_QuickBooksEmployeeId",
                table: "Employee");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_RenoCompanyID_QuickBooksEmployeeId",
                table: "Employee",
                columns: new[] { "RenoCompanyID", "QuickBooksEmployeeId" },
                unique: true,
                filter: "\"QuickBooksEmployeeId\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employee_RenoCompanyID_QuickBooksEmployeeId",
                table: "Employee");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_RenoCompanyID_QuickBooksEmployeeId",
                table: "Employee",
                columns: new[] { "RenoCompanyID", "QuickBooksEmployeeId" },
                unique: true);
        }
    }
}
