using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankTransactionMetadataTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTransactionMetadataKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactionMetadataKeys", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "BankTransactionMetadataValues",
                columns: table => new
                {
                    BankTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KeyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StringValue = table.Column<string>(type: "TEXT", nullable: true),
                    IntegerValue = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_BankTransactionMetadataValues",
                        x => new { x.BankTransactionId, x.KeyId }
                    );
                    table.CheckConstraint(
                        "CK_BankTransactionMetadataValues_Value_Exactly_One",
                        "(\"StringValue\" IS NULL) <> (\"IntegerValue\" IS NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_BankTransactionMetadataValues_BankTransactionMetadataKeys_KeyId",
                        column: x => x.KeyId,
                        principalTable: "BankTransactionMetadataKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_BankTransactionMetadataValues_BankTransactions_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "UX_BankTransactionMetadataKeys_Name",
                table: "BankTransactionMetadataKeys",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMetadataValues_KeyId",
                table: "BankTransactionMetadataValues",
                column: "KeyId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BankTransactionMetadataValues");

            migrationBuilder.DropTable(name: "BankTransactionMetadataKeys");
        }
    }
}
