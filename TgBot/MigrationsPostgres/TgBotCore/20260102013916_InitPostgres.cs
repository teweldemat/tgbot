using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.MigrationsPostgres.TgBotCore
{
    public partial class InitPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: true),
                    ParentRecord = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    TransactionHead = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MisDelta",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true),
                    DataType = table.Column<string>(type: "text", nullable: true),
                    RecordNo = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MisDelta", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "MisUserProfile",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    Permitted = table.Column<bool>(type: "boolean", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MisUserProfile", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "WorkFlowMain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Creator = table.Column<string>(type: "text", nullable: true),
                    WorkType = table.Column<int>(type: "integer", nullable: false),
                    WorkItemHead = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkItemHeadTime = table.Column<long>(type: "bigint", nullable: true),
                    WorkItemHeadType = table.Column<int>(type: "integer", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowMain", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkFlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrevItem = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkType = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItem", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecord");

            migrationBuilder.DropTable(
                name: "CashEntity");

            migrationBuilder.DropTable(
                name: "MisDelta");

            migrationBuilder.DropTable(
                name: "MisUserProfile");

            migrationBuilder.DropTable(
                name: "WorkFlowMain");

            migrationBuilder.DropTable(
                name: "WorkItem");
        }
    }
}
