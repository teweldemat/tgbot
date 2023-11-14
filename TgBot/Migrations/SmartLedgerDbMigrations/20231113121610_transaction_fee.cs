using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class transaction_fee : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PayeeFee",
                table: "PaymentSource",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PayerFee",
                table: "PaymentSource",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayeeFee",
                table: "PaymentSource");

            migrationBuilder.DropColumn(
                name: "PayerFee",
                table: "PaymentSource");
        }
    }
}
