using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanConstructionDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionDepositAccountId",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConstructionDepositAnnualRatePercent",
                table: "Loans",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionDepositInterestIncomeAccountId",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_ConstructionDepositAccountId",
                table: "Loans",
                column: "ConstructionDepositAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_ConstructionDepositInterestIncomeAccountId",
                table: "Loans",
                column: "ConstructionDepositInterestIncomeAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Accounts_ConstructionDepositAccountId",
                table: "Loans",
                column: "ConstructionDepositAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Accounts_ConstructionDepositInterestIncomeAccountId",
                table: "Loans",
                column: "ConstructionDepositInterestIncomeAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Accounts_ConstructionDepositAccountId",
                table: "Loans");

            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Accounts_ConstructionDepositInterestIncomeAccountId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loans_ConstructionDepositAccountId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loans_ConstructionDepositInterestIncomeAccountId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ConstructionDepositAccountId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ConstructionDepositAnnualRatePercent",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ConstructionDepositInterestIncomeAccountId",
                table: "Loans");
        }
    }
}
