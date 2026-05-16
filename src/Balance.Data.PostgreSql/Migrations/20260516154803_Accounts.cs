using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Accounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    AccountType = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    CurrencyCode = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: false
                    ),
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
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[]
                {
                    "Id",
                    "AccountType",
                    "CreatedAt",
                    "CurrencyCode",
                    "Name",
                    "UpdatedAt",
                },
                values: new object[]
                {
                    new Guid("00000000-0000-7000-8000-000000000001"),
                    "Equity",
                    new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                    "EUR",
                    "Opening Balances",
                    new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CurrencyCode",
                table: "Accounts",
                column: "CurrencyCode"
            );

            // Functional unique index for global case-insensitive uniqueness on Account.Name.
            // EF Core's standard CreateIndex cannot express LOWER(...) so this is hand-rolled.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_Accounts_Name\" ON \"Accounts\" (LOWER(\"Name\"));"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Accounts");
        }
    }
}
