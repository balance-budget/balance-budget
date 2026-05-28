using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountTypeCardIdentifierImporterKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BankAccounts_IbanOrAccountNumber",
                table: "BankAccounts"
            );

            migrationBuilder.AddColumn<string>(
                name: "CardIdentifier",
                table: "BankAccounts",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ImporterKey",
                table: "BankAccounts",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "BankAccounts",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Current"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_BankAccounts_CardOwnedOnly",
                table: "BankAccounts",
                sql: "\"Type\" <> 'Card' OR \"AccountId\" IS NOT NULL"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_BankAccounts_IdentifierByType",
                table: "BankAccounts",
                sql: "(\"Type\" = 'Current' AND \"Iban\" IS NOT NULL) OR (\"Type\" = 'Savings' AND (\"Iban\" IS NOT NULL OR \"AccountNumber\" IS NOT NULL)) OR (\"Type\" = 'Card' AND \"CardIdentifier\" IS NOT NULL)"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BankAccounts_CardOwnedOnly",
                table: "BankAccounts"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_BankAccounts_IdentifierByType",
                table: "BankAccounts"
            );

            migrationBuilder.DropColumn(name: "CardIdentifier", table: "BankAccounts");

            migrationBuilder.DropColumn(name: "ImporterKey", table: "BankAccounts");

            migrationBuilder.DropColumn(name: "Type", table: "BankAccounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BankAccounts_IbanOrAccountNumber",
                table: "BankAccounts",
                sql: "\"Iban\" IS NOT NULL OR \"AccountNumber\" IS NOT NULL"
            );
        }
    }
}
