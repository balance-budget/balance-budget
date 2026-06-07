using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountIsLiquid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited from the scaffolded `defaultValue: false`: existing accounts must come
            // out Liquid (the model default), not Illiquid. The default only backfills existing
            // rows — the model carries no store default, so inserts always send an explicit value.
            migrationBuilder.AddColumn<bool>(
                name: "IsLiquid",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsLiquid", table: "Accounts");
        }
    }
}
