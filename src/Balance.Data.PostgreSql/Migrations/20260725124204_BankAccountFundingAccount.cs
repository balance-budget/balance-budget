using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountFundingAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FundingBankAccountId",
                table: "BankAccounts",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_FundingBankAccountId",
                table: "BankAccounts",
                column: "FundingBankAccountId"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_BankAccounts_FundingCardOnly",
                table: "BankAccounts",
                sql: "\"FundingBankAccountId\" IS NULL OR (\"Type\" = 'Card' AND \"FundingBankAccountId\" <> \"Id\")"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_BankAccounts_FundingBankAccountId",
                table: "BankAccounts",
                column: "FundingBankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_BankAccounts_FundingBankAccountId",
                table: "BankAccounts"
            );

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_FundingBankAccountId",
                table: "BankAccounts"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_BankAccounts_FundingCardOnly",
                table: "BankAccounts"
            );

            migrationBuilder.DropColumn(name: "FundingBankAccountId", table: "BankAccounts");
        }
    }
}
