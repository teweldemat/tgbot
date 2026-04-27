using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.MigrationsPostgres.TgBotContext
{
    public partial class InitPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JoinedTGGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TgGroupId = table.Column<long>(type: "bigint", nullable: false),
                    BotId = table.Column<string>(type: "text", nullable: true),
                    JoinedTime = table.Column<long>(type: "bigint", nullable: false),
                    LeftTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinedTGGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TgUserState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TgUserID = table.Column<string>(type: "text", nullable: true),
                    TgBotId = table.Column<string>(type: "text", nullable: true),
                    Data = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTime = table.Column<long>(type: "bigint", nullable: false),
                    StackHead = table.Column<Guid>(type: "uuid", nullable: true),
                    WaitingForPayment = table.Column<bool>(type: "boolean", nullable: false),
                    WbcCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TgUserState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WFDialogStack",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    TgUserId = table.Column<string>(type: "text", nullable: true),
                    BotId = table.Column<string>(type: "text", nullable: true),
                    Next = table.Column<Guid>(type: "uuid", nullable: true),
                    DataType = table.Column<string>(type: "text", nullable: true),
                    Data = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Removed = table.Column<bool>(type: "boolean", nullable: false)
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
