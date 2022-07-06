using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class mis_1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "PaymentUserProfile", schema: "dbo", newName: "MisUserProfile", newSchema: "dbo");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "MisUserProfile", schema: "dbo", newName: "PaymentUserProfile", newSchema: "dbo");
        }
    }
}
