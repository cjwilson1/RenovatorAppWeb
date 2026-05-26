using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMileageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MileageTracking",
                columns: table => new
                {
                    UniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalMileage = table.Column<double>(type: "double precision", nullable: false),
                    TotalTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    StartingLocation = table.Column<string>(type: "text", nullable: false),
                    StartingPosition = table.Column<string>(type: "text", nullable: false),
                    EndingLocation = table.Column<string>(type: "text", nullable: false),
                    EndingPosition = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MileageTracking", x => x.UniqueId);
                });

            migrationBuilder.CreateTable(
                name: "MileageTrackingWaypoint",
                columns: table => new
                {
                    UniqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    MileageTrackingId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaypointTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GpsCoordinates = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MileageTrackingWaypoint", x => x.UniqueId);
                    table.ForeignKey(
                        name: "FK_MileageTrackingWaypoint_MileageTracking_MileageTrackingId",
                        column: x => x.MileageTrackingId,
                        principalTable: "MileageTracking",
                        principalColumn: "UniqueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MileageTracking_TrackingStartedAtUtc",
                table: "MileageTracking",
                column: "TrackingStartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MileageTrackingWaypoint_MileageTrackingId",
                table: "MileageTrackingWaypoint",
                column: "MileageTrackingId");

            migrationBuilder.CreateIndex(
                name: "IX_MileageTrackingWaypoint_WaypointTime",
                table: "MileageTrackingWaypoint",
                column: "WaypointTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MileageTrackingWaypoint");

            migrationBuilder.DropTable(
                name: "MileageTracking");
        }
    }
}
