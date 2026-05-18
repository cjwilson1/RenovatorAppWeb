using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260517133000_AddQuickBooksCustomers")]
    public partial class AddQuickBooksCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                table: "Address",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Address",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CountrySubDivisionCode",
                table: "Address",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Street3",
                table: "Address",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuickBooksCustomerId = table.Column<string>(type: "text", nullable: false),
                    SyncToken = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    FullyQualifiedName = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    GivenName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    FamilyName = table.Column<string>(type: "text", nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: false),
                    PrintOnCheckName = table.Column<string>(type: "text", nullable: false),
                    PrimaryEmailAddress = table.Column<string>(type: "text", nullable: false),
                    PrimaryPhone = table.Column<string>(type: "text", nullable: false),
                    AlternatePhone = table.Column<string>(type: "text", nullable: false),
                    MobilePhone = table.Column<string>(type: "text", nullable: false),
                    Fax = table.Column<string>(type: "text", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Taxable = table.Column<bool>(type: "boolean", nullable: false),
                    Job = table.Column<bool>(type: "boolean", nullable: false),
                    BillWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    BalanceWithJobs = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PreferredDeliveryMethod = table.Column<string>(type: "text", nullable: false),
                    ParentRefValue = table.Column<string>(type: "text", nullable: false),
                    ParentRefName = table.Column<string>(type: "text", nullable: false),
                    PaymentMethodRefValue = table.Column<string>(type: "text", nullable: false),
                    PaymentMethodRefName = table.Column<string>(type: "text", nullable: false),
                    SalesTermRefValue = table.Column<string>(type: "text", nullable: false),
                    SalesTermRefName = table.Column<string>(type: "text", nullable: false),
                    CurrencyRefValue = table.Column<string>(type: "text", nullable: false),
                    CurrencyRefName = table.Column<string>(type: "text", nullable: false),
                    QuickBooksCreateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuickBooksLastUpdatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BillAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipAddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLinkDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEditDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_Customer_Address_BillAddressId",
                        column: x => x.BillAddressId,
                        principalTable: "Address",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Customer_Address_ShipAddressId",
                        column: x => x.ShipAddressId,
                        principalTable: "Address",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_BillAddressId",
                table: "Customer",
                column: "BillAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_CompanyName",
                table: "Customer",
                column: "CompanyName");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_DisplayName",
                table: "Customer",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_QuickBooksCustomerId",
                table: "Customer",
                column: "QuickBooksCustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_ShipAddressId",
                table: "Customer",
                column: "ShipAddressId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Address");

            migrationBuilder.DropColumn(
                name: "CountrySubDivisionCode",
                table: "Address");

            migrationBuilder.DropColumn(
                name: "Street3",
                table: "Address");

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                table: "Address",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
