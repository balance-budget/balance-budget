using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankTransactionDismissalMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                table: "BankTransactions",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "DismissedReason",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_BankTransactions_Dismissed_Pair",
                table: "BankTransactions",
                sql: "(\"DismissedAt\" IS NULL AND \"DismissedReason\" IS NULL) OR (\"DismissedAt\" IS NOT NULL AND \"DismissedReason\" IS NOT NULL)"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BankTransactions_Dismissed_Pair",
                table: "BankTransactions"
            );

            migrationBuilder.DropColumn(name: "DismissedAt", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "DismissedReason", table: "BankTransactions");
        }
    }
}
