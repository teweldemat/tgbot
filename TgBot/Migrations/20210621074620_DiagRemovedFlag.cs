using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations
{
    public partial class DiagRemovedFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Removed",
                table: "WFDialogStack",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Removed",
                table: "WFDialogStack");
        }
    }
}
