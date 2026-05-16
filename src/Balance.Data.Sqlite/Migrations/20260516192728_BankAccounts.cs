using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    AccountNumber = table.Column<string>(
                        type: "TEXT",
                        maxLength: 64,
                        nullable: true
                    ),
                    Bic = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    BankName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AccountHolderName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 128,
                        nullable: true
                    ),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CounterpartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.CheckConstraint(
                        "CK_BankAccounts_IbanOrAccountNumber",
                        "\"Iban\" IS NOT NULL OR \"AccountNumber\" IS NOT NULL"
                    );
                    table.CheckConstraint(
                        "CK_BankAccounts_OwnershipXor",
                        "(\"AccountId\" IS NULL) <> (\"CounterpartyId\" IS NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_BankAccounts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_BankAccounts_Counterparties_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "Counterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_BankAccounts_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_AccountId",
                table: "BankAccounts",
                column: "AccountId",
                unique: true,
                filter: "\"AccountId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CounterpartyId",
                table: "BankAccounts",
                column: "CounterpartyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CurrencyCode",
                table: "BankAccounts",
                column: "CurrencyCode"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_Iban",
                table: "BankAccounts",
                column: "Iban",
                unique: true,
                filter: "\"Iban\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BankAccounts");
        }
    }
}
