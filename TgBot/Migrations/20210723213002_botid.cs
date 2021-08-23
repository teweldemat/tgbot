using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations
{
    public partial class botid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_JoinedTGGroup",
                table: "JoinedTGGroup");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "JoinedTGGroup",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BotId",
                table: "JoinedTGGroup",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_JoinedTGGroup",
                table: "JoinedTGGroup",
                column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_JoinedTGGroup",
                table: "JoinedTGGroup");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "JoinedTGGroup");

            migrationBuilder.DropColumn(
                name: "BotId",
                table: "JoinedTGGroup");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JoinedTGGroup",
                table: "JoinedTGGroup",
                column: "TgGroupId");
        }
    }
}
