using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations
{
    public partial class botid_2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TgUserState",
                table: "TgUserState");

            migrationBuilder.AddColumn<string>(
                name: "BotId",
                table: "WFDialogStack",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TgUserID",
                table: "TgUserState",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "TgUserState",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.NewGuid());
            migrationBuilder.Sql("Update TgUserState set Id=NEWID()");
                

            migrationBuilder.AddColumn<string>(
                name: "TgBotId",
                table: "TgUserState",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TgUserState",
                table: "TgUserState",
                column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TgUserState",
                table: "TgUserState");

            migrationBuilder.DropColumn(
                name: "BotId",
                table: "WFDialogStack");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TgUserState");

            migrationBuilder.DropColumn(
                name: "TgBotId",
                table: "TgUserState");

            migrationBuilder.AlterColumn<string>(
                name: "TgUserID",
                table: "TgUserState",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TgUserState",
                table: "TgUserState",
                column: "TgUserID");
        }
    }
}
