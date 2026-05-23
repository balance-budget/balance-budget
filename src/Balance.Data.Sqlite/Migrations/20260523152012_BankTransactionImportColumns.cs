using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BankTransactionImportColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CounterpartyAccountNumber",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyName",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 256,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "BankTransactions",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "RawSource",
                table: "BankTransactions",
                type: "TEXT",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "RowHash",
                table: "BankTransactions",
                type: "TEXT",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.CreateIndex(
                name: "UX_BankTransactions_BankAccountId_RowHash",
                table: "BankTransactions",
                columns: new[] { "BankAccountId", "RowHash" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_BankTransactions_BankAccountId_RowHash",
                table: "BankTransactions"
            );

            migrationBuilder.DropColumn(
                name: "CounterpartyAccountNumber",
                table: "BankTransactions"
            );

            migrationBuilder.DropColumn(name: "CounterpartyName", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "Description", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "RawSource", table: "BankTransactions");

            migrationBuilder.DropColumn(name: "RowHash", table: "BankTransactions");
        }
    }
}
