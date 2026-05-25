using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class JournalEntryBankTransactionIdUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_BankTransactionId",
                table: "JournalEntries"
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_BankTransactionId",
                table: "JournalEntries",
                column: "BankTransactionId",
                unique: true,
                filter: "\"BankTransactionId\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_BankTransactionId",
                table: "JournalEntries"
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_BankTransactionId",
                table: "JournalEntries",
                column: "BankTransactionId"
            );
        }
    }
}
