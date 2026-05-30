using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationAndRenoCompanyTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultCompanyID = new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.DropIndex(
                name: "IX_Settings_Name",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_PartSource_Name",
                table: "PartSource");

            migrationBuilder.DropIndex(
                name: "IX_InspectionAreaType_Name",
                table: "InspectionAreaType");

            migrationBuilder.DropIndex(
                name: "IX_InspectionAreaCategory_Name",
                table: "InspectionAreaCategory");

            migrationBuilder.DropIndex(
                name: "IX_Employee_QuickBooksEmployeeId",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_Customer_QuickBooksCustomerId",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_BuildingType_Name",
                table: "BuildingType");

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Settings",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Property",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "PartSource",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Part",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "MileageTrackingWaypoint",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "MileageTracking",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Inspectors",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "InspectionAreaType",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "InspectionAreaNotePhoto",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "InspectionAreaNoteEstimateItem",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "InspectionAreaNote",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "InspectionAreaCategory",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "InspectionArea",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Inspection",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Employee",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Documents",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Customer",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "BuildingType",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Building",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.AddColumn<Guid>(
                name: "RenoCompanyID",
                table: "Address",
                type: "uuid",
                nullable: false,
                defaultValue: defaultCompanyID);

            migrationBuilder.CreateTable(
                name: "RenoCompany",
                columns: table => new
                {
                    RenoCompanyID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StreetAddress = table.Column<string>(type: "text", nullable: false),
                    StreetAddress2 = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Zip = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Fax = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    URL = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenoCompany", x => x.RenoCompanyID);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    RoleID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.RoleID);
                });

            migrationBuilder.Sql("""
                INSERT INTO "RenoCompany" ("RenoCompanyID", "Name", "StreetAddress", "StreetAddress2", "City", "State", "Zip", "Phone", "Fax", "Email", "URL", "Active", "DateCreated")
                VALUES ('11111111-1111-1111-1111-111111111111', 'RenovatorApp', '', '', '', '', '', '', '', '', '', true, NOW())
                ON CONFLICT ("RenoCompanyID") DO NOTHING;

                INSERT INTO "Role" ("RoleID", "Name")
                VALUES
                    ('22222222-2222-2222-2222-222222222221', 'User'),
                    ('22222222-2222-2222-2222-222222222222', 'Admin'),
                    ('22222222-2222-2222-2222-222222222223', 'SuperAdmin')
                ON CONFLICT ("RoleID") DO NOTHING;
                """);

            migrationBuilder.CreateTable(
                name: "RenoUser",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    RenoCompanyID = table.Column<Guid>(type: "uuid", nullable: false),
                    Login = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PhonePrimary = table.Column<string>(type: "text", nullable: false),
                    PhoneSecondary = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateLastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenoUser", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_RenoUser_RenoCompany_RenoCompanyID",
                        column: x => x.RenoCompanyID,
                        principalTable: "RenoCompany",
                        principalColumn: "RenoCompanyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserID, x.RoleID });
                    table.ForeignKey(
                        name: "FK_UserRole_RenoUser_UserID",
                        column: x => x.UserID,
                        principalTable: "RenoUser",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleID",
                        column: x => x.RoleID,
                        principalTable: "Role",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Settings_RenoCompanyID_Name",
                table: "Settings",
                columns: new[] { "RenoCompanyID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartSource_RenoCompanyID_Name",
                table: "PartSource",
                columns: new[] { "RenoCompanyID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Part_RenoCompanyID",
                table: "Part",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_MileageTracking_RenoCompanyID",
                table: "MileageTracking",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAreaType_RenoCompanyID_Name",
                table: "InspectionAreaType",
                columns: new[] { "RenoCompanyID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAreaCategory_RenoCompanyID_Name",
                table: "InspectionAreaCategory",
                columns: new[] { "RenoCompanyID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_RenoCompanyID",
                table: "Inspection",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_RenoCompanyID",
                table: "Employee",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_RenoCompanyID_QuickBooksEmployeeId",
                table: "Employee",
                columns: new[] { "RenoCompanyID", "QuickBooksEmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RenoCompanyID",
                table: "Documents",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_RenoCompanyID",
                table: "Customer",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_RenoCompanyID_QuickBooksCustomerId",
                table: "Customer",
                columns: new[] { "RenoCompanyID", "QuickBooksCustomerId" },
                unique: true,
                filter: "\"QuickBooksCustomerId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingType_RenoCompanyID_Name",
                table: "BuildingType",
                columns: new[] { "RenoCompanyID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenoCompany_Name",
                table: "RenoCompany",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RenoUser_Login",
                table: "RenoUser",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenoUser_RenoCompanyID",
                table: "RenoUser",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Name",
                table: "Role",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleID",
                table: "UserRole",
                column: "RoleID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "RenoUser");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "RenoCompany");

            migrationBuilder.DropIndex(
                name: "IX_Settings_RenoCompanyID_Name",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_PartSource_RenoCompanyID_Name",
                table: "PartSource");

            migrationBuilder.DropIndex(
                name: "IX_Part_RenoCompanyID",
                table: "Part");

            migrationBuilder.DropIndex(
                name: "IX_MileageTracking_RenoCompanyID",
                table: "MileageTracking");

            migrationBuilder.DropIndex(
                name: "IX_InspectionAreaType_RenoCompanyID_Name",
                table: "InspectionAreaType");

            migrationBuilder.DropIndex(
                name: "IX_InspectionAreaCategory_RenoCompanyID_Name",
                table: "InspectionAreaCategory");

            migrationBuilder.DropIndex(
                name: "IX_Inspection_RenoCompanyID",
                table: "Inspection");

            migrationBuilder.DropIndex(
                name: "IX_Employee_RenoCompanyID",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_Employee_RenoCompanyID_QuickBooksEmployeeId",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_Documents_RenoCompanyID",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Customer_RenoCompanyID",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Customer_RenoCompanyID_QuickBooksCustomerId",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_BuildingType_RenoCompanyID_Name",
                table: "BuildingType");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Property");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "PartSource");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Part");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "MileageTrackingWaypoint");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "MileageTracking");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Inspectors");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "InspectionAreaType");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "InspectionAreaNotePhoto");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "InspectionAreaNoteEstimateItem");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "InspectionAreaNote");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "InspectionAreaCategory");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "InspectionArea");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Inspection");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "BuildingType");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Building");

            migrationBuilder.DropColumn(
                name: "RenoCompanyID",
                table: "Address");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Name",
                table: "Settings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartSource_Name",
                table: "PartSource",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAreaType_Name",
                table: "InspectionAreaType",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionAreaCategory_Name",
                table: "InspectionAreaCategory",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employee_QuickBooksEmployeeId",
                table: "Employee",
                column: "QuickBooksEmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_QuickBooksCustomerId",
                table: "Customer",
                column: "QuickBooksCustomerId",
                unique: true,
                filter: "\"QuickBooksCustomerId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingType_Name",
                table: "BuildingType",
                column: "Name",
                unique: true);
        }
    }
}
