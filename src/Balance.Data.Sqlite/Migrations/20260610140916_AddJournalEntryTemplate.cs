using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntryTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CounterAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CounterpartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Cadence = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AnchorDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpectedAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    MandateId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SepaCreditorId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 64,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryTemplates_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_JournalEntryTemplates_Accounts_CounterAccountId",
                        column: x => x.CounterAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_JournalEntryTemplates_Counterparties_CounterpartyId",
                        column: x => x.CounterpartyId,
                        principalTable: "Counterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplates_AccountId",
                table: "JournalEntryTemplates",
                column: "AccountId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplates_CounterAccountId",
                table: "JournalEntryTemplates",
                column: "CounterAccountId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplates_CounterpartyId",
                table: "JournalEntryTemplates",
                column: "CounterpartyId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "JournalEntryTemplates");
        }
    }
}
