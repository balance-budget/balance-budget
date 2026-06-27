using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class MergeIngImporterKeysToLogical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Collapse the version-suffixed ING importer keys to their logical form (ADR 0034):
            // the BankAccount binds to a logical importer and the concrete statement layout is
            // resolved per-file by content sniffing. The credit-card V1/V2 merge is lossy, so
            // Down is a no-op.
            migrationBuilder.Sql(
                "UPDATE \"BankAccounts\" SET \"ImporterKey\" = 'Ing.CurrentAccount' WHERE \"ImporterKey\" = 'Ing.CurrentAccount.V1';"
            );
            migrationBuilder.Sql(
                "UPDATE \"BankAccounts\" SET \"ImporterKey\" = 'Ing.SavingsAccount' WHERE \"ImporterKey\" = 'Ing.SavingsAccount.V1';"
            );
            migrationBuilder.Sql(
                "UPDATE \"BankAccounts\" SET \"ImporterKey\" = 'Ing.CreditCard' WHERE \"ImporterKey\" IN ('Ing.CreditCard.V1', 'Ing.CreditCard.V2');"
            );
            migrationBuilder.Sql(
                "UPDATE \"BankTransactions\" SET \"ImporterKey\" = 'Ing.CurrentAccount' WHERE \"ImporterKey\" = 'Ing.CurrentAccount.V1';"
            );
            migrationBuilder.Sql(
                "UPDATE \"BankTransactions\" SET \"ImporterKey\" = 'Ing.SavingsAccount' WHERE \"ImporterKey\" = 'Ing.SavingsAccount.V1';"
            );
            migrationBuilder.Sql(
                "UPDATE \"BankTransactions\" SET \"ImporterKey\" = 'Ing.CreditCard' WHERE \"ImporterKey\" IN ('Ing.CreditCard.V1', 'Ing.CreditCard.V2');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
