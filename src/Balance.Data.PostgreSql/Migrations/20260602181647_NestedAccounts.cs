using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Balance.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class NestedAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Accounts_Name", table: "Accounts");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsPostable",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<Guid>(
                name: "ParentAccountId",
                table: "Accounts",
                type: "uuid",
                nullable: true
            );

            // Backfill Code from Name for every pre-existing row. Name was globally unique before
            // this migration, so this yields unique, non-empty codes — safe to index below. New
            // installs only have the seed row, which the UpdateData below sets explicitly.
            migrationBuilder.Sql(
                "UPDATE \"Accounts\" SET \"Code\" = \"Name\" WHERE \"Code\" = '';"
            );

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-7000-8000-000000000001"),
                columns: new[] { "Code", "IsPostable", "ParentAccountId" },
                values: new object[] { "3900", true, null }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Code",
                table: "Accounts",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId",
                table: "Accounts",
                column: "ParentAccountId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Accounts_ParentAccountId",
                table: "Accounts",
                column: "ParentAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Accounts_ParentAccountId",
                table: "Accounts"
            );

            migrationBuilder.DropIndex(name: "IX_Accounts_Code", table: "Accounts");

            migrationBuilder.DropIndex(name: "IX_Accounts_ParentAccountId", table: "Accounts");

            migrationBuilder.DropColumn(name: "Code", table: "Accounts");

            migrationBuilder.DropColumn(name: "IsPostable", table: "Accounts");

            migrationBuilder.DropColumn(name: "ParentAccountId", table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Name",
                table: "Accounts",
                column: "Name",
                unique: true
            );
        }
    }
}
