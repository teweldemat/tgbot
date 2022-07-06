using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class subtaskcount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChildCount",
                table: "MisTask",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("Update MisTask set ChildCount=(Select count(*) from MisTask t where t.ParentTaskId=MisTask.id)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChildCount",
                table: "MisTask");
        }
    }
}
