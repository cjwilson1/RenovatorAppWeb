using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RenovatorApp.Infrastructure.Data;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    [DbContext(typeof(RenovatorAppDbContext))]
    [Migration("20260526210000_AddMileageTrackingWaypointLocation")]
    public partial class AddMileageTrackingWaypointLocation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "MileageTrackingWaypoint",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location",
                table: "MileageTrackingWaypoint");
        }
    }
}
