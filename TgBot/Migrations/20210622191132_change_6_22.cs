using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations
{
    public partial class change_6_22 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TgUserName",
                table: "TgUserState");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TgUserName",
                table: "TgUserState",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
