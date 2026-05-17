using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartPackageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPackage",
                table: "Part",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PackageUnits",
                table: "Part",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Part",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPackage",
                table: "Part");

            migrationBuilder.DropColumn(
                name: "PackageUnits",
                table: "Part");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "Part");
        }
    }
}
