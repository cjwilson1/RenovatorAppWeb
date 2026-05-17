using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260517124500_AddPartImageUrl")]
    public partial class AddPartImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Part",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Part");
        }
    }
}
