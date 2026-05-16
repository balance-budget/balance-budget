using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: false
                    ),
                    Name = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    MinorUnitScale = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                }
            );

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                }
            );

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Code", "MinorUnitScale", "Name", "Symbol" },
                values: new object[,]
                {
                    { "BTC", 8, "Bitcoin", "₿" },
                    { "CHF", 2, "Swiss Franc", "CHF" },
                    { "ETH", 18, "Ether", "Ξ" },
                    { "EUR", 2, "Euro", "€" },
                    { "GBP", 2, "Pound Sterling", "£" },
                    { "JPY", 0, "Japanese Yen", "¥" },
                    { "USD", 2, "United States Dollar", "$" },
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Currencies");

            migrationBuilder.DropTable(name: "DataProtectionKeys");
        }
    }
}
