using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace TgBot.MigrationsPostgres.SmartLedger
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
                name: "CashAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAccount", x => x.Id);
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
                name: "CashLedgerEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgerEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderN = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoneTime = table.Column<long>(type: "bigint", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DutyStation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyStation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DutyStationSchedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    DutyStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleType = table.Column<string>(type: "text", nullable: true),
                    ScheduleData = table.Column<string>(type: "text", nullable: true),
                    SetTime = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyStationSchedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowReportVersion",
                columns: table => new
                {
                    FromTime = table.Column<long>(type: "bigint", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ToTime = table.Column<long>(type: "bigint", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowReportVersion", x => x.FromTime);
                });

            migrationBuilder.CreateTable(
                name: "Holiday",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTime = table.Column<long>(type: "bigint", nullable: false),
                    ToTime = table.Column<long>(type: "bigint", nullable: false),
                    HolidayName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holiday", x => x.Id);
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
                name: "MisTask",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    CreateTime = table.Column<long>(type: "bigint", nullable: false),
                    UpdateTime = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PlannedStartTime = table.Column<long>(type: "bigint", nullable: true),
                    PlannedEndTime = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<long>(type: "bigint", nullable: true),
                    EndTime = table.Column<long>(type: "bigint", nullable: true),
                    WaitingSince = table.Column<long>(type: "bigint", nullable: true),
                    WaitingFor = table.Column<string>(type: "text", nullable: true),
                    AuidtId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChildCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MisTask", x => x.Id);
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
                name: "OnDutyCheck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Eta = table.Column<long>(type: "bigint", nullable: true),
                    SelfReported = table.Column<bool>(type: "boolean", nullable: false),
                    CheckType = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnDutyCheck", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    ToPayTo = table.Column<string>(type: "text", nullable: true),
                    TransferTo = table.Column<Guid>(type: "uuid", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    WorkItemHead = table.Column<Guid>(type: "uuid", nullable: true),
                    HeadTime = table.Column<long>(type: "bigint", nullable: true),
                    HeadType = table.Column<int>(type: "integer", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    Creator = table.Column<string>(type: "text", nullable: true),
                    Reference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentFlowRule",
                columns: table => new
                {
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<string>(type: "text", nullable: true),
                    Rule = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentFlowRule", x => x.EntityId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSource",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    PaymentInstruction = table.Column<string>(type: "text", nullable: true),
                    PayerFee = table.Column<long>(type: "bigint", nullable: false),
                    PayeeFee = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSource", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWorkItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_PaymentWorkItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<long>(type: "bigint", nullable: false),
                    AccountBalance = table.Column<long>(type: "bigint", nullable: false),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    WorkItemHead = table.Column<Guid>(type: "uuid", nullable: true),
                    HeadTime = table.Column<long>(type: "bigint", nullable: true),
                    HeadType = table.Column<int>(type: "integer", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    Creator = table.Column<string>(type: "text", nullable: true),
                    Reference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reconciliation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationWorkItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ReconciliationWorkItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskComment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplyOf = table.Column<Guid>(type: "uuid", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    AuidtId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderN = table.Column<int>(type: "integer", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImgeMime = table.Column<string>(type: "text", nullable: true),
                    LinkType = table.Column<int>(type: "integer", nullable: false),
                    ContentLink = table.Column<string>(type: "text", nullable: true),
                    Caption = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskDelta",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDelta", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "TaskEntityConfiguration",
                columns: table => new
                {
                    EntityID = table.Column<Guid>(type: "uuid", nullable: false),
                    Config = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskEntityConfiguration", x => x.EntityID);
                });

            migrationBuilder.CreateTable(
                name: "TaskUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<string>(type: "text", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedTime = table.Column<long>(type: "bigint", nullable: false),
                    LeftTime = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskWorkType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    OrderN = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskWorkType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    PrevTransaction = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    Payment = table.Column<Guid>(type: "uuid", nullable: true),
                    ReverseRole = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDutyStation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    DutyStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedTime = table.Column<long>(type: "bigint", nullable: false),
                    LeftTime = table.Column<long>(type: "bigint", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDutyStation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDutyStationException",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    remoteWork = table.Column<bool>(type: "boolean", nullable: false),
                    FromTime = table.Column<long>(type: "bigint", nullable: false),
                    ToTime = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDutyStationException", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDutyStationStatus",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DutyStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckInTime = table.Column<long>(type: "bigint", nullable: true),
                    CheckOutTime = table.Column<long>(type: "bigint", nullable: true),
                    BreakTime = table.Column<long>(type: "bigint", nullable: true),
                    OutOfficeTaskTime = table.Column<long>(type: "bigint", nullable: true),
                    DontDesturbTime = table.Column<long>(type: "bigint", nullable: true),
                    RunningLateTime = table.Column<long>(type: "bigint", nullable: true),
                    NotComingTime = table.Column<long>(type: "bigint", nullable: true),
                    Eta = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDutyStationStatus", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserTaskType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    OrderN = table.Column<int>(type: "integer", nullable: false),
                    WorkTypeId = table.Column<int>(type: "integer", nullable: false),
                    AuditId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTaskType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkState",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    StateData = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkState", x => x.UserId);
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

            migrationBuilder.CreateTable(
                name: "WorkItemPicture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderN = table.Column<int>(type: "integer", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImgeMime = table.Column<string>(type: "text", nullable: true),
                    LinkedImage = table.Column<string>(type: "text", nullable: true),
                    LinkedImageType = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemPicture", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaitingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaitingForTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaitForTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    WaitingSince = table.Column<long>(type: "bigint", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "AuditRecord");

            migrationBuilder.DropTable(
                name: "CashAccount");

            migrationBuilder.DropTable(
                name: "CashEntity");

            migrationBuilder.DropTable(
                name: "CashLedgerEntry");

            migrationBuilder.DropTable(
                name: "CheckLists");

            migrationBuilder.DropTable(
                name: "DutyStation");

            migrationBuilder.DropTable(
                name: "DutyStationSchedule");

            migrationBuilder.DropTable(
                name: "FlowReportVersion");

            migrationBuilder.DropTable(
                name: "Holiday");

            migrationBuilder.DropTable(
                name: "MisDelta");

            migrationBuilder.DropTable(
                name: "MisUserProfile");

            migrationBuilder.DropTable(
                name: "OnDutyCheck");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "PaymentFlowRule");

            migrationBuilder.DropTable(
                name: "PaymentSource");

            migrationBuilder.DropTable(
                name: "PaymentWorkItem");

            migrationBuilder.DropTable(
                name: "Reconciliation");

            migrationBuilder.DropTable(
                name: "ReconciliationWorkItem");

            migrationBuilder.DropTable(
                name: "TaskComment");

            migrationBuilder.DropTable(
                name: "TaskContents");

            migrationBuilder.DropTable(
                name: "TaskDelta");

            migrationBuilder.DropTable(
                name: "TaskEntityConfiguration");

            migrationBuilder.DropTable(
                name: "TaskUsers");

            migrationBuilder.DropTable(
                name: "TaskWorkType");

            migrationBuilder.DropTable(
                name: "Transaction");

            migrationBuilder.DropTable(
                name: "UserDutyStation");

            migrationBuilder.DropTable(
                name: "UserDutyStationException");

            migrationBuilder.DropTable(
                name: "UserDutyStationStatus");

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
                name: "WorkItemPicture");

            migrationBuilder.DropTable(
                name: "MisTask");
        }
    }
}
