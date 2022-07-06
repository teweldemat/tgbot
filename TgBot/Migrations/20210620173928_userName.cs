using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations
{
    public partial class userName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TgUserName",
                table: "TgUserState",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TgUserName",
                table: "TgUserState");
        }
    }
}
