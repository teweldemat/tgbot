using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class task_dsstatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuditId",
                table: "OnDutyCheck",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "UserDutyStationStatus",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DutyStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInTime = table.Column<long>(type: "bigint", nullable: true),
                    CheckOutTime = table.Column<long>(type: "bigint", nullable: true),
                    BreakTime = table.Column<long>(type: "bigint", nullable: true),
                    OutOfficeTaskTime = table.Column<long>(type: "bigint", nullable: true),
                    DontDesturbTime = table.Column<long>(type: "bigint", nullable: true),
                    RunningLateTime = table.Column<long>(type: "bigint", nullable: true),
                    NotComingTime = table.Column<long>(type: "bigint", nullable: true),
                    Eta = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDutyStationStatus", x => x.UserId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDutyStationStatus");

            migrationBuilder.DropColumn(
                name: "AuditId",
                table: "OnDutyCheck");
        }
    }
}
