using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
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
                type: "character varying(64)",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ImporterKey",
                table: "BankAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "BankAccounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Current"
            );

            // Backfill: existing BankAccounts whose only identifier is AccountNumber are the
            // credit-card BankAccounts created before ADR 0019 (PAN was stuffed into
            // AccountNumber to satisfy the legacy CHECK). Reclassify them as Card and copy the
            // value into CardIdentifier so the new CK_BankAccounts_IdentifierByType holds. We
            // intentionally leave AccountNumber populated — the legacy CHECK is still in scope
            // until the new constraints are added; AccountNumber on a Card BankAccount is
            // harmless duplicate data after this migration since CardIdentifier is the
            // canonical column the extractor matches against.
            migrationBuilder.Sql(
                "UPDATE \"BankAccounts\" SET \"Type\" = 'Card', \"CardIdentifier\" = \"AccountNumber\" "
                    + "WHERE \"Iban\" IS NULL AND \"AccountNumber\" IS NOT NULL"
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
