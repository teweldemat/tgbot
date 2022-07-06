using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class UserProfile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Creator",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HeadTime",
                table: "Payment",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeadType",
                table: "Payment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToPayTo",
                table: "Payment",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentUserProfile",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentUserProfile", x => x.UserId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentUserProfile");

            migrationBuilder.DropColumn(
                name: "Creator",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "HeadTime",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "HeadType",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ToPayTo",
                table: "Payment");
        }
    }
}
