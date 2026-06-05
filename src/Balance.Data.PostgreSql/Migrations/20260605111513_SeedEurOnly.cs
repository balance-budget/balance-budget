using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SeedEurOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Currencies", keyColumn: "Code", keyValue: "BTC");

            migrationBuilder.DeleteData(table: "Currencies", keyColumn: "Code", keyValue: "CHF");

            migrationBuilder.DeleteData(table: "Currencies", keyColumn: "Code", keyValue: "ETH");

            migrationBuilder.DeleteData(table: "Currencies", keyColumn: "Code", keyValue: "GBP");

            migrationBuilder.DeleteData(table: "Currencies", keyColumn: "Code", keyValue: "JPY");

            migrationBuilder.DeleteData(table: "Currencies", keyColumn: "Code", keyValue: "USD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Code", "MinorUnitScale", "Name", "Symbol" },
                values: new object[,]
                {
                    { "BTC", 8, "Bitcoin", "₿" },
                    { "CHF", 2, "Swiss Franc", "CHF" },
                    { "ETH", 18, "Ether", "Ξ" },
                    { "GBP", 2, "Pound Sterling", "£" },
                    { "JPY", 0, "Japanese Yen", "¥" },
                    { "USD", 2, "United States Dollar", "$" },
                }
            );
        }
    }
}
