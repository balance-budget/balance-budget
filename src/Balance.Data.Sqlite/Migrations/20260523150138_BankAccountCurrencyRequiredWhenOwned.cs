using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountCurrencyRequiredWhenOwned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_BankAccounts_CurrencyRequiredWhenOwned",
                table: "BankAccounts",
                sql: "\"AccountId\" IS NULL OR \"CurrencyCode\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BankAccounts_CurrencyRequiredWhenOwned",
                table: "BankAccounts"
            );
        }
    }
}
