using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RenovatorApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameCustomerLastLinkDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    "LastLinkDate" timestamp with time zone NULL,
                    "LastEditDate" timestamp with time zone NULL,
                    CONSTRAINT "PK_Customer" PRIMARY KEY ("CustomerId")
                );

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Customer'
                          AND column_name = 'LastLinkDate'
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Customer'
                          AND column_name = 'LastSyncDate'
                    )
                    THEN
                        ALTER TABLE "Customer" RENAME COLUMN "LastLinkDate" TO "LastSyncDate";
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastSyncDate",
                table: "Customer",
                newName: "LastLinkDate");
        }
    }
}
