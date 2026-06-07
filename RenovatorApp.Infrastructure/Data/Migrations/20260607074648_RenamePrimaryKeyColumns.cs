using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePrimaryKeyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Settings",
                newName: "AppSettingId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Property",
                newName: "PropertyId");

            migrationBuilder.RenameColumn(
                name: "UniqueId",
                table: "MileageTrackingWaypoint",
                newName: "MileageTrackingWaypointId");

            migrationBuilder.RenameColumn(
                name: "UniqueId",
                table: "MileageTracking",
                newName: "MileageTrackingID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Inspectors",
                newName: "InspectorId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "InspectionAreaNotePhoto",
                newName: "InspectionAreaNotePhotoId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "InspectionAreaNoteEstimateItem",
                newName: "InspectionAreaNoteEstimateItemId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "InspectionAreaNote",
                newName: "InspectionAreaNoteId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "InspectionAreaCategory",
                newName: "InspectionAreaCategoryId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "InspectionArea",
                newName: "InspectionAreaId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Inspection",
                newName: "InspectionId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "BuildingType",
                newName: "BuildingTypeId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Building",
                newName: "BuildingId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Address",
                newName: "AddressId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AppSettingId",
                table: "Settings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "PropertyId",
                table: "Property",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "MileageTrackingWaypointId",
                table: "MileageTrackingWaypoint",
                newName: "UniqueId");

            migrationBuilder.RenameColumn(
                name: "MileageTrackingID",
                table: "MileageTracking",
                newName: "UniqueId");

            migrationBuilder.RenameColumn(
                name: "InspectorId",
                table: "Inspectors",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "InspectionAreaNotePhotoId",
                table: "InspectionAreaNotePhoto",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "InspectionAreaNoteEstimateItemId",
                table: "InspectionAreaNoteEstimateItem",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "InspectionAreaNoteId",
                table: "InspectionAreaNote",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "InspectionAreaCategoryId",
                table: "InspectionAreaCategory",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "InspectionAreaId",
                table: "InspectionArea",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "InspectionId",
                table: "Inspection",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "BuildingTypeId",
                table: "BuildingType",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "BuildingId",
                table: "Building",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "AddressId",
                table: "Address",
                newName: "Id");
        }
    }
}
