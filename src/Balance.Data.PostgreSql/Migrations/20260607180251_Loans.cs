using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Loans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LoanPartId",
                table: "JournalLines",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    LenderCounterpartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterestExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loans_Accounts_InterestExpenseAccountId",
                        column: x => x.InterestExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Loans_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Loans_Counterparties_LenderCounterpartyId",
                        column: x => x.LenderCounterpartyId,
                        principalTable: "Counterparties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LoanParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    RepaymentType = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_LoanParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanParts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_LoanParts_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LoanPartRatePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanPartId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnualRatePercent = table.Column<decimal>(
                        type: "numeric(8,4)",
                        precision: 8,
                        scale: 4,
                        nullable: false
                    ),
                    FixedUntil = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LoanPartRatePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanPartRatePeriods_LoanParts_LoanPartId",
                        column: x => x.LoanPartId,
                        principalTable: "LoanParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_LoanPartId",
                table: "JournalLines",
                column: "LoanPartId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LoanPartRatePeriods_LoanPartId_EffectiveDate",
                table: "LoanPartRatePeriods",
                columns: new[] { "LoanPartId", "EffectiveDate" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_LoanParts_AccountId",
                table: "LoanParts",
                column: "AccountId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_LoanParts_LoanId",
                table: "LoanParts",
                column: "LoanId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Loans_InterestExpenseAccountId",
                table: "Loans",
                column: "InterestExpenseAccountId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Loans_LenderCounterpartyId",
                table: "Loans",
                column: "LenderCounterpartyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Loans_ParentAccountId",
                table: "Loans",
                column: "ParentAccountId",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_JournalLines_LoanParts_LoanPartId",
                table: "JournalLines",
                column: "LoanPartId",
                principalTable: "LoanParts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalLines_LoanParts_LoanPartId",
                table: "JournalLines"
            );

            migrationBuilder.DropTable(name: "LoanPartRatePeriods");

            migrationBuilder.DropTable(name: "LoanParts");

            migrationBuilder.DropTable(name: "Loans");

            migrationBuilder.DropIndex(name: "IX_JournalLines_LoanPartId", table: "JournalLines");

            migrationBuilder.DropColumn(name: "LoanPartId", table: "JournalLines");
        }
    }
}
