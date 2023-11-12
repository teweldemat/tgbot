using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class reconciliation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkItemHead = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HeadTime = table.Column<long>(type: "bigint", nullable: true),
                    HeadType = table.Column<int>(type: "int", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Creator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reconciliation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationWorkItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrevItem = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<string>(type: "ntext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationWorkItem", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reconciliation");

            migrationBuilder.DropTable(
                name: "ReconciliationWorkItem");
        }
    }
}
