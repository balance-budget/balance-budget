using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Iban = table.Column<string>(
                        type: "character varying(34)",
                        maxLength: 34,
                        nullable: true
                    ),
                    AccountNumber = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    Bic = table.Column<string>(
                        type: "character varying(11)",
                        maxLength: 11,
                        nullable: true
                    ),
                    BankName = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    AccountHolderName = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    CurrencyCode = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: true
                    ),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
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
