using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankTransactionPromotedMetadataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "BankTransactions",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "ForeignAmount",
                table: "BankTransactions",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ForeignCurrencyCode",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 8,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ImporterKey",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "MandateId",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "SepaCreditorId",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValueDate",
                table: "BankTransactions",
                type: "TEXT",
                nullable: true
            );

            // The ING current-account extractor is the only one wired up today, so
            // every existing row is its output. Stamp them so the upcoming RawSource
            // re-extraction (ADR 0015) knows which extractor to dispatch to.
            migrationBuilder.Sql(
                "UPDATE \"BankTransactions\" SET \"ImporterKey\" = 'Ing.CurrentAccount.V1' "
                    + "WHERE \"ImporterKey\" IS NULL;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExchangeRate", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "ForeignAmount", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "ForeignCurrencyCode", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "ImporterKey", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "MandateId", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "Reference", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "SepaCreditorId", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "ValueDate", table: "BankTransactions");
        }
    }
}
