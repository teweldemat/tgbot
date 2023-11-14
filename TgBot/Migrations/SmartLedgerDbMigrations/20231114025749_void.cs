using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class @void : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReverseRole",
                table: "Transaction",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReverseRole",
                table: "Transaction");
        }
    }
}
