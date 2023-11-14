using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class ledger_display_order : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "CashLedgerEntry",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "CashLedgerEntry");
        }
    }
}
