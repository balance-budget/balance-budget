using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class OwnedBankAccountIdentifierUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_AccountNumber_Owned",
                table: "BankAccounts",
                column: "AccountNumber",
                unique: true,
                filter: "\"AccountNumber\" IS NOT NULL AND \"AccountId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CardIdentifier_Owned",
                table: "BankAccounts",
                column: "CardIdentifier",
                unique: true,
                filter: "\"CardIdentifier\" IS NOT NULL AND \"AccountId\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_AccountNumber_Owned",
                table: "BankAccounts"
            );

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CardIdentifier_Owned",
                table: "BankAccounts"
            );
        }
    }
}
