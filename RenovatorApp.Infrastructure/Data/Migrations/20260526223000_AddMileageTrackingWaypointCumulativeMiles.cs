using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RenovatorApp.Infrastructure.Data;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    [DbContext(typeof(RenovatorAppDbContext))]
    [Migration("20260526223000_AddMileageTrackingWaypointCumulativeMiles")]
    public partial class AddMileageTrackingWaypointCumulativeMiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CumulativeMiles",
                table: "MileageTrackingWaypoint",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CumulativeMiles",
                table: "MileageTrackingWaypoint");
        }
    }
}
