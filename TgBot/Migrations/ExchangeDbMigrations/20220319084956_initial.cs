using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TgBot.Migrations.ExchangeDbMigrations
{
    public partial class initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetType",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    OneUnit = table.Column<long>(type: "bigint", nullable: false),
                    MinPrice = table.Column<long>(type: "bigint", nullable: false),
                    MaxPrice = table.Column<long>(type: "bigint", nullable: false),
                    MinOffer = table.Column<long>(type: "bigint", nullable: false),
                    MaxOffer = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetType", x => x.Key);
                });

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
                name: "BankAccountType",
                columns: table => new
                {
                    BankId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    WeeklyTransactionCountLimit = table.Column<int>(type: "integer", nullable: true),
                    SingleTransactionAmountLimit = table.Column<long>(type: "bigint", nullable: true),
                    WeeklyTransactionAmountLimit = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccountType", x => x.BankId);
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
                name: "ExchangeOffer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateTranId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateTranId = table.Column<Guid>(type: "uuid", nullable: false),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    AssetKey = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeOffer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    TransactionType = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeTransaction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeTransactionData",
                columns: table => new
                {
                    TranId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeTransactionData", x => x.TranId);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeUserProfile",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TranId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    EMail = table.Column<string>(type: "text", nullable: true),
                    EmailVarified = table.Column<bool>(type: "boolean", nullable: false),
                    UserStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeUserProfile", x => x.UserId);
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
                name: "OfferBankAccount",
                columns: table => new
                {
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<string>(type: "text", nullable: false),
                    AccountName = table.Column<string>(type: "text", nullable: true),
                    AccountNumber = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferBankAccount", x => new { x.OfferId, x.BankId });
                });

            migrationBuilder.CreateTable(
                name: "OfferStatusHistory",
                columns: table => new
                {
                    TranId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrevTranId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferStatusHistory", x => x.TranId);
                });

            migrationBuilder.CreateTable(
                name: "TrusteeApplication",
                columns: table => new
                {
                    TranId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateTranId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrusteeApplication", x => x.TranId);
                });

            migrationBuilder.CreateTable(
                name: "TrusteeBankAccount",
                columns: table => new
                {
                    TrusteId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<string>(type: "text", nullable: false),
                    AccountName = table.Column<string>(type: "text", nullable: true),
                    AccountNumber = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Remark = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrusteeBankAccount", x => new { x.TrusteId, x.BankId });
                });

            migrationBuilder.CreateTable(
                name: "TrusteeInformation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    CreateTranId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateTranId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationStatus = table.Column<int>(type: "integer", nullable: false),
                    Remark = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrusteeInformation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkFlowInfo",
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
                    table.PrimaryKey("PK_WorkFlowInfo", x => x.Id);
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

            migrationBuilder.InsertData(
                table: "AssetType",
                columns: new[] { "Key", "MaxOffer", "MaxPrice", "MinOffer", "MinPrice", "Name", "OneUnit" },
                values: new object[,]
                {
                    { "USDT", 1000000L, 100000000L, 100L, 1L, "Tether", 100L },
                    { "BTC", 1000000L, 100000000L, 100L, 1L, "Bitcoin", 100L },
                    { "ETH", 1000000L, 100000000L, 100L, 1L, "Etherium", 100L }
                });

            migrationBuilder.InsertData(
                table: "BankAccountType",
                columns: new[] { "BankId", "Name", "SingleTransactionAmountLimit", "WeeklyTransactionAmountLimit", "WeeklyTransactionCountLimit" },
                values: new object[,]
                {
                    { "CBE", "Commercial Bank of Ethiopia", -1L, -1L, -11 },
                    { "ABYS", "Commercial Bank of Ethiopia", -1L, -1L, -11 },
                    { "DASH", "Dashen Bank", -1L, -1L, -11 },
                    { "CBO", "Cooperative Bank of Oromia", -1L, -1L, -11 },
                    { "HBR", "Bibret Bank", -1L, -1L, -11 },
                    { "ZMN", "Zemen Bank", -1L, -1L, -11 }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetType");

            migrationBuilder.DropTable(
                name: "AuditRecord");

            migrationBuilder.DropTable(
                name: "BankAccountType");

            migrationBuilder.DropTable(
                name: "CashEntity");

            migrationBuilder.DropTable(
                name: "ExchangeOffer");

            migrationBuilder.DropTable(
                name: "ExchangeTransaction");

            migrationBuilder.DropTable(
                name: "ExchangeTransactionData");

            migrationBuilder.DropTable(
                name: "ExchangeUserProfile");

            migrationBuilder.DropTable(
                name: "MisDelta");

            migrationBuilder.DropTable(
                name: "MisUserProfile");

            migrationBuilder.DropTable(
                name: "OfferBankAccount");

            migrationBuilder.DropTable(
                name: "OfferStatusHistory");

            migrationBuilder.DropTable(
                name: "TrusteeApplication");

            migrationBuilder.DropTable(
                name: "TrusteeBankAccount");

            migrationBuilder.DropTable(
                name: "TrusteeInformation");

            migrationBuilder.DropTable(
                name: "WorkFlowInfo");

            migrationBuilder.DropTable(
                name: "WorkItem");
        }
    }
}
