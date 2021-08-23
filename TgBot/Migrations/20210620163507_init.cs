using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations
{
    public partial class init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JoinedTGGroup",
                columns: table => new
                {
                    TgGroupId = table.Column<long>(type: "bigint", nullable: false),
                    JoinedTime = table.Column<long>(type: "bigint", nullable: false),
                    LeftTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinedTGGroup", x => x.TgGroupId);
                });

            migrationBuilder.CreateTable(
                name: "TgUserState",
                columns: table => new
                {
                    TgUserID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdateTime = table.Column<long>(type: "bigint", nullable: false),
                    StackHead = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaitingForPayment = table.Column<bool>(type: "bit", nullable: false),
                    WbcCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TgUserState", x => x.TgUserID);
                });

            migrationBuilder.CreateTable(
                name: "WFDialogStack",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TgUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Next = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WFDialogStack", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinedTGGroup");

            migrationBuilder.DropTable(
                name: "TgUserState");

            migrationBuilder.DropTable(
                name: "WFDialogStack");
        }
    }
}
