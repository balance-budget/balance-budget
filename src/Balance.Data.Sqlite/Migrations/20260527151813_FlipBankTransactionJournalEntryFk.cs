using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class FlipBankTransactionJournalEntryFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "BankTransactions",
                type: "TEXT",
                nullable: true
            );

            // Backfill the new FK from the old direction before dropping it.
            migrationBuilder.Sql(
                """
                UPDATE "BankTransactions"
                SET "JournalEntryId" = (
                    SELECT "Id" FROM "JournalEntries"
                    WHERE "JournalEntries"."BankTransactionId" = "BankTransactions"."Id"
                )
                WHERE EXISTS (
                    SELECT 1 FROM "JournalEntries"
                    WHERE "JournalEntries"."BankTransactionId" = "BankTransactions"."Id"
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_JournalEntryId",
                table: "BankTransactions",
                column: "JournalEntryId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_JournalEntries_JournalEntryId",
                table: "BankTransactions",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_BankTransactions_BankTransactionId",
                table: "JournalEntries"
            );

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_BankTransactionId",
                table: "JournalEntries"
            );

            migrationBuilder.DropColumn(name: "BankTransactionId", table: "JournalEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankTransactionId",
                table: "JournalEntries",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "JournalEntries"
                SET "BankTransactionId" = (
                    SELECT "Id" FROM "BankTransactions"
                    WHERE "BankTransactions"."JournalEntryId" = "JournalEntries"."Id"
                )
                WHERE EXISTS (
                    SELECT 1 FROM "BankTransactions"
                    WHERE "BankTransactions"."JournalEntryId" = "JournalEntries"."Id"
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_BankTransactionId",
                table: "JournalEntries",
                column: "BankTransactionId",
                unique: true,
                filter: "\"BankTransactionId\" IS NOT NULL"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_BankTransactions_BankTransactionId",
                table: "JournalEntries",
                column: "BankTransactionId",
                principalTable: "BankTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_JournalEntries_JournalEntryId",
                table: "BankTransactions"
            );

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_JournalEntryId",
                table: "BankTransactions"
            );

            migrationBuilder.DropColumn(name: "JournalEntryId", table: "BankTransactions");
        }
    }
}
