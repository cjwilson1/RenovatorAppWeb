using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceClientWithCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspection_Client_ClientId",
                table: "Inspection");

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "Customer" (
                    "CustomerId" uuid NOT NULL,
                    "QuickBooksCustomerId" text NOT NULL DEFAULT '',
                    "SyncToken" text NOT NULL DEFAULT '',
                    "DisplayName" text NOT NULL DEFAULT '',
                    "FullyQualifiedName" text NOT NULL DEFAULT '',
                    "CompanyName" text NOT NULL DEFAULT '',
                    "Title" text NOT NULL DEFAULT '',
                    "GivenName" text NOT NULL DEFAULT '',
                    "MiddleName" text NOT NULL DEFAULT '',
                    "FamilyName" text NOT NULL DEFAULT '',
                    "Suffix" text NOT NULL DEFAULT '',
                    "PrintOnCheckName" text NOT NULL DEFAULT '',
                    "PrimaryEmailAddress" text NOT NULL DEFAULT '',
                    "PrimaryPhone" text NOT NULL DEFAULT '',
                    "AlternatePhone" text NOT NULL DEFAULT '',
                    "MobilePhone" text NOT NULL DEFAULT '',
                    "Fax" text NOT NULL DEFAULT '',
                    "Website" text NOT NULL DEFAULT '',
                    "Notes" text NOT NULL DEFAULT '',
                    "Active" boolean NOT NULL DEFAULT TRUE,
                    "Taxable" boolean NOT NULL DEFAULT FALSE,
                    "Job" boolean NOT NULL DEFAULT FALSE,
                    "BillWithParent" boolean NOT NULL DEFAULT FALSE,
                    "Balance" numeric(12,2) NOT NULL DEFAULT 0,
                    "BalanceWithJobs" numeric(12,2) NOT NULL DEFAULT 0,
                    "PreferredDeliveryMethod" text NOT NULL DEFAULT '',
                    "ParentRefValue" text NOT NULL DEFAULT '',
                    "ParentRefName" text NOT NULL DEFAULT '',
                    "PaymentMethodRefValue" text NOT NULL DEFAULT '',
                    "PaymentMethodRefName" text NOT NULL DEFAULT '',
                    "SalesTermRefValue" text NOT NULL DEFAULT '',
                    "SalesTermRefName" text NOT NULL DEFAULT '',
                    "CurrencyRefValue" text NOT NULL DEFAULT '',
                    "CurrencyRefName" text NOT NULL DEFAULT '',
                    "QuickBooksCreateTime" timestamp with time zone NULL,
                    "QuickBooksLastUpdatedTime" timestamp with time zone NULL,
                    "BillAddressId" uuid NULL,
                    "ShipAddressId" uuid NULL,
                    "CreatedDate" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "LastSyncDate" timestamp with time zone NULL,
                    "LastEditDate" timestamp with time zone NULL,
                    CONSTRAINT "PK_Customer" PRIMARY KEY ("CustomerId")
                );

                DROP INDEX IF EXISTS "IX_Customer_QuickBooksCustomerId";

                ALTER TABLE "Address" ALTER COLUMN "PropertyId" DROP NOT NULL;
                ALTER TABLE "Address" ADD COLUMN IF NOT EXISTS "Street3" text NOT NULL DEFAULT '';
                ALTER TABLE "Address" ADD COLUMN IF NOT EXISTS "CountrySubDivisionCode" text NOT NULL DEFAULT '';
                ALTER TABLE "Address" ADD COLUMN IF NOT EXISTS "Country" text NOT NULL DEFAULT '';
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Address" ("Id", "PropertyId", "Street1", "Street2", "Street3", "City", "State", "CountrySubDivisionCode", "PostalCode", "Country")
                SELECT
                    (substr(md5("ClientId"::text || ':bill'), 1, 8) || '-' ||
                     substr(md5("ClientId"::text || ':bill'), 9, 4) || '-' ||
                     substr(md5("ClientId"::text || ':bill'), 13, 4) || '-' ||
                     substr(md5("ClientId"::text || ':bill'), 17, 4) || '-' ||
                     substr(md5("ClientId"::text || ':bill'), 21, 12))::uuid,
                    NULL,
                    "Street1",
                    "Street2",
                    '',
                    "City",
                    "State",
                    '',
                    "PostalCode",
                    ''
                FROM "Client"
                WHERE "Street1" <> '' OR "Street2" <> '' OR "City" <> '' OR "State" <> '' OR "PostalCode" <> ''
                ON CONFLICT ("Id") DO UPDATE SET
                    "Street1" = EXCLUDED."Street1",
                    "Street2" = EXCLUDED."Street2",
                    "City" = EXCLUDED."City",
                    "State" = EXCLUDED."State",
                    "PostalCode" = EXCLUDED."PostalCode";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Customer" (
                    "CustomerId",
                    "QuickBooksCustomerId",
                    "SyncToken",
                    "DisplayName",
                    "FullyQualifiedName",
                    "CompanyName",
                    "Title",
                    "GivenName",
                    "MiddleName",
                    "FamilyName",
                    "Suffix",
                    "PrintOnCheckName",
                    "PrimaryEmailAddress",
                    "PrimaryPhone",
                    "AlternatePhone",
                    "MobilePhone",
                    "Fax",
                    "Website",
                    "Notes",
                    "Active",
                    "Taxable",
                    "Job",
                    "BillWithParent",
                    "Balance",
                    "BalanceWithJobs",
                    "PreferredDeliveryMethod",
                    "ParentRefValue",
                    "ParentRefName",
                    "PaymentMethodRefValue",
                    "PaymentMethodRefName",
                    "SalesTermRefValue",
                    "SalesTermRefName",
                    "CurrencyRefValue",
                    "CurrencyRefName",
                    "QuickBooksCreateTime",
                    "QuickBooksLastUpdatedTime",
                    "BillAddressId",
                    "ShipAddressId",
                    "CreatedDate",
                    "LastSyncDate",
                    "LastEditDate")
                SELECT
                    "ClientId",
                    '',
                    '',
                    COALESCE(NULLIF(trim("FirstName" || ' ' || "LastName"), ''), NULLIF("CompanyName", ''), 'Unnamed Customer'),
                    COALESCE(NULLIF(trim("FirstName" || ' ' || "LastName"), ''), NULLIF("CompanyName", ''), 'Unnamed Customer'),
                    "CompanyName",
                    '',
                    "FirstName",
                    '',
                    "LastName",
                    '',
                    '',
                    "Email",
                    "Phone",
                    '',
                    '',
                    '',
                    '',
                    "Notes",
                    TRUE,
                    FALSE,
                    FALSE,
                    FALSE,
                    0,
                    0,
                    '',
                    '',
                    '',
                    '',
                    '',
                    '',
                    '',
                    '',
                    '',
                    NULL,
                    NULL,
                    CASE
                        WHEN "Street1" <> '' OR "Street2" <> '' OR "City" <> '' OR "State" <> '' OR "PostalCode" <> ''
                        THEN (substr(md5("ClientId"::text || ':bill'), 1, 8) || '-' ||
                              substr(md5("ClientId"::text || ':bill'), 9, 4) || '-' ||
                              substr(md5("ClientId"::text || ':bill'), 13, 4) || '-' ||
                              substr(md5("ClientId"::text || ':bill'), 17, 4) || '-' ||
                              substr(md5("ClientId"::text || ':bill'), 21, 12))::uuid
                        ELSE NULL
                    END,
                    NULL,
                    NOW(),
                    NULL,
                    NULL
                FROM "Client"
                ON CONFLICT ("CustomerId") DO UPDATE SET
                    "DisplayName" = EXCLUDED."DisplayName",
                    "FullyQualifiedName" = EXCLUDED."FullyQualifiedName",
                    "CompanyName" = EXCLUDED."CompanyName",
                    "GivenName" = EXCLUDED."GivenName",
                    "FamilyName" = EXCLUDED."FamilyName",
                    "PrimaryEmailAddress" = EXCLUDED."PrimaryEmailAddress",
                    "PrimaryPhone" = EXCLUDED."PrimaryPhone",
                    "Notes" = EXCLUDED."Notes",
                    "Active" = TRUE,
                    "BillAddressId" = EXCLUDED."BillAddressId";
                """);

            migrationBuilder.DropTable(
                name: "Client");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "Inspection",
                newName: "CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Inspection_ClientId",
                table: "Inspection",
                newName: "IX_Inspection_CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_FamilyName",
                table: "Customer",
                column: "FamilyName");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_QuickBooksCustomerId",
                table: "Customer",
                column: "QuickBooksCustomerId",
                unique: true,
                filter: "\"QuickBooksCustomerId\" <> ''");

            migrationBuilder.AddForeignKey(
                name: "FK_Inspection_Customer_CustomerId",
                table: "Inspection",
                column: "CustomerId",
                principalTable: "Customer",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspection_Customer_CustomerId",
                table: "Inspection");

            migrationBuilder.DropIndex(
                name: "IX_Customer_FamilyName",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Customer_QuickBooksCustomerId",
                table: "Customer");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Inspection",
                newName: "ClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Inspection_CustomerId",
                table: "Inspection",
                newName: "IX_Inspection_ClientId");

            migrationBuilder.CreateTable(
                name: "Client",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Street1 = table.Column<string>(type: "text", nullable: false),
                    Street2 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Client", x => x.ClientId);
                });

            migrationBuilder.Sql("""
                INSERT INTO "Client" ("ClientId", "FirstName", "LastName", "CompanyName", "Phone", "Email", "Street1", "Street2", "City", "State", "PostalCode", "Notes")
                SELECT
                    customer."CustomerId",
                    customer."GivenName",
                    customer."FamilyName",
                    customer."CompanyName",
                    customer."PrimaryPhone",
                    customer."PrimaryEmailAddress",
                    COALESCE(address."Street1", ''),
                    COALESCE(address."Street2", ''),
                    COALESCE(address."City", ''),
                    COALESCE(address."State", ''),
                    COALESCE(address."PostalCode", ''),
                    customer."Notes"
                FROM "Customer" customer
                LEFT JOIN "Address" address ON address."Id" = customer."BillAddressId"
                WHERE EXISTS (
                    SELECT 1
                    FROM "Inspection" inspection
                    WHERE inspection."ClientId" = customer."CustomerId"
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM "Customer"
                WHERE "QuickBooksCustomerId" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_QuickBooksCustomerId",
                table: "Customer",
                column: "QuickBooksCustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Client_CompanyName",
                table: "Client",
                column: "CompanyName");

            migrationBuilder.CreateIndex(
                name: "IX_Client_LastName",
                table: "Client",
                column: "LastName");

            migrationBuilder.AddForeignKey(
                name: "FK_Inspection_Client_ClientId",
                table: "Inspection",
                column: "ClientId",
                principalTable: "Client",
                principalColumn: "ClientId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
