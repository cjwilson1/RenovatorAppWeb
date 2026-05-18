using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickBooksEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employee",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuickBooksEmployeeId = table.Column<string>(type: "text", nullable: false),
                    SyncToken = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    PrintOnCheckName = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    GivenName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    FamilyName = table.Column<string>(type: "text", nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: false),
                    PrimaryEmailAddress = table.Column<string>(type: "text", nullable: false),
                    PrimaryPhone = table.Column<string>(type: "text", nullable: false),
                    MobilePhone = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    BillableTime = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: false),
                    Organization = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    HiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BillRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    HourlyCostRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QuickBooksCreateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuickBooksLastUpdatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrimaryAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEditDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employee", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_Employee_Address_PrimaryAddressId",
                        column: x => x.PrimaryAddressId,
                        principalTable: "Address",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employee_DisplayName",
                table: "Employee",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_FamilyName",
                table: "Employee",
                column: "FamilyName");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_PrimaryAddressId",
                table: "Employee",
                column: "PrimaryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_QuickBooksEmployeeId",
                table: "Employee",
                column: "QuickBooksEmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employee");
        }
    }
}
