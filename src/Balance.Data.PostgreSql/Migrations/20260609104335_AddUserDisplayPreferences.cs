using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDisplayPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "NumberFormat",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DateFormat", table: "AspNetUsers");

            migrationBuilder.DropColumn(name: "Language", table: "AspNetUsers");

            migrationBuilder.DropColumn(name: "NumberFormat", table: "AspNetUsers");
        }
    }
}
