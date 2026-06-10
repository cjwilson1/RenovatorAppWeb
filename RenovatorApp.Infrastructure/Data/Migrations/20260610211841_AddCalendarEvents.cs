using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvent",
                columns: table => new
                {
                    CalendarEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenoCompanyID = table.Column<Guid>(type: "uuid", nullable: false),
                    RenoUserID = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EventAlertTimes = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvent", x => x.CalendarEventId);
                    table.ForeignKey(
                        name: "FK_CalendarEvent_Inspection_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "Inspection",
                        principalColumn: "InspectionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CalendarEvent_RenoUser_RenoUserID",
                        column: x => x.RenoUserID,
                        principalTable: "RenoUser",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_Date",
                table: "CalendarEvent",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_InspectionId",
                table: "CalendarEvent",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_RenoCompanyID",
                table: "CalendarEvent",
                column: "RenoCompanyID");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_RenoUserID",
                table: "CalendarEvent",
                column: "RenoUserID");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_UpdatedAtUtc",
                table: "CalendarEvent",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvent");
        }
    }
}
