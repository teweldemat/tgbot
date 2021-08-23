using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class task_misc_4 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskData",
                table: "TaskData");

            migrationBuilder.RenameTable(
                name: "TaskData",
                newName: "TaskDelta");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskDelta",
                table: "TaskDelta",
                column: "AuditId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskDelta",
                table: "TaskDelta");

            migrationBuilder.RenameTable(
                name: "TaskDelta",
                newName: "TaskData");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskData",
                table: "TaskData",
                column: "TaskId");
        }
    }
}
