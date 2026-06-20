using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountHorizon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Horizon",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "ShortTerm"
            );

            // One-time backfill (ADR-0030): derive each existing account's Horizon. Illiquid money
            // is held for the decade (Long-term); a liquid account fed by a Savings bank account is
            // reserves for the year (Medium-term); everything else is day-to-day money (Short-term).
            migrationBuilder.Sql(
                """
                UPDATE "Accounts" SET "Horizon" = CASE
                    WHEN "IsLiquid" = false THEN 'LongTerm'
                    WHEN "Id" IN (
                        SELECT "AccountId" FROM "BankAccounts"
                        WHERE "Type" = 'Savings' AND "AccountId" IS NOT NULL
                    ) THEN 'MediumTerm'
                    ELSE 'ShortTerm'
                END;
                """
            );

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000001"),
                column: "Horizon",
                value: "ShortTerm"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Horizon", table: "Accounts");
        }
    }
}
