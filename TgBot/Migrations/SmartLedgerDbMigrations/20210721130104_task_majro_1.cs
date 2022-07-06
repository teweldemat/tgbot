using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.SmartLedgerDbMigrations
{
    public partial class task_majro_1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderN = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DutyStation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyStation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DutyStationSchedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DutyStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduleData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetTime = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyStationSchedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holiday",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromTime = table.Column<long>(type: "bigint", nullable: false),
                    ToTime = table.Column<long>(type: "bigint", nullable: false),
                    HolidayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holiday", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MisTask",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateTime = table.Column<long>(type: "bigint", nullable: false),
                    UpdateTime = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlannedStartTime = table.Column<long>(type: "bigint", nullable: true),
                    PlannedEndTime = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<long>(type: "bigint", nullable: true),
                    EndTime = table.Column<long>(type: "bigint", nullable: true),
                    WaitingSince = table.Column<long>(type: "bigint", nullable: true),
                    AuidtId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MisTask", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnDutyCheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Eta = table.Column<long>(type: "bigint", nullable: true),
                    SelfReported = table.Column<bool>(type: "bit", nullable: false),
                    CheckType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnDutyCheck", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderN = table.Column<int>(type: "int", nullable: false),
                    Image = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImgeMime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkType = table.Column<int>(type: "int", nullable: false),
                    ContentLink = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskEntityConfiguration",
                columns: table => new
                {
                    EntityID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskEntityConfiguration", x => x.EntityID);
                });

            migrationBuilder.CreateTable(
                name: "TaskUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedTime = table.Column<long>(type: "bigint", nullable: false),
                    LeftTime = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskWorkType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderN = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskWorkType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDutyStation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DutyStationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedTime = table.Column<long>(type: "bigint", nullable: false),
                    LeftTime = table.Column<long>(type: "bigint", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDutyStation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDutyStationException",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    remoteWork = table.Column<bool>(type: "bit", nullable: false),
                    FromTime = table.Column<long>(type: "bigint", nullable: false),
                    ToTime = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDutyStationException", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTaskType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderN = table.Column<int>(type: "int", nullable: false),
                    WorkTypeId = table.Column<int>(type: "int", nullable: false),
                    AuditId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTaskType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkState",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StateData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkState", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "WorkFlowMain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Creator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    WorkItemHead = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkItemHeadTime = table.Column<long>(type: "bigint", nullable: true),
                    WorkItemHeadType = table.Column<int>(type: "int", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkFlowMain", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkFlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrevItem = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaitingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitingForTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitForTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaitingSince = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_WaitingTasks_MisTask_WaitForTaskId",
                        column: x => x.WaitForTaskId,
                        principalTable: "MisTask",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "TaskWorkType",
                columns: new[] { "Id", "Name", "OrderN" },
                values: new object[,]
                {
                    { 1, "Technical", 1 },
                    { 2, "Administrative", 2 },
                    { 3, "Business Development", 3 },
                    { 99, "Others", 99 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingTasks_WaitForTaskId",
                table: "WaitingTasks",
                column: "WaitForTaskId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckLists");

            migrationBuilder.DropTable(
                name: "DutyStation");

            migrationBuilder.DropTable(
                name: "DutyStationSchedule");

            migrationBuilder.DropTable(
                name: "Holiday");

            migrationBuilder.DropTable(
                name: "OnDutyCheck");

            migrationBuilder.DropTable(
                name: "TaskContents");

            migrationBuilder.DropTable(
                name: "TaskEntityConfiguration");

            migrationBuilder.DropTable(
                name: "TaskUsers");

            migrationBuilder.DropTable(
                name: "TaskWorkType");

            migrationBuilder.DropTable(
                name: "UserDutyStation");

            migrationBuilder.DropTable(
                name: "UserDutyStationException");

            migrationBuilder.DropTable(
                name: "UserTaskType");

            migrationBuilder.DropTable(
                name: "UserWorkState");

            migrationBuilder.DropTable(
                name: "WaitingTasks");

            migrationBuilder.DropTable(
                name: "WorkFlowMain");

            migrationBuilder.DropTable(
                name: "WorkItem");

            migrationBuilder.DropTable(
                name: "MisTask");
        }
    }
}
