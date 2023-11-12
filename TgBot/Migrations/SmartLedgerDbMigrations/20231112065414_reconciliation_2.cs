using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class reconciliation_2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AccountBalance",
                table: "Reconciliation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountBalance",
                table: "Reconciliation");
        }
    }
}
