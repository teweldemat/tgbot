using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class WorkItemPicture_rename : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemPictures");

            migrationBuilder.CreateTable(
                name: "WorkItemPicture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderN = table.Column<int>(type: "int", nullable: false),
                    Image = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImgeMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedImageType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemPicture", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemPicture");

            migrationBuilder.CreateTable(
                name: "WorkItemPictures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Image = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImgeMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedImageType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderN = table.Column<int>(type: "int", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemPictures", x => x.Id);
                });
        }
    }
}
