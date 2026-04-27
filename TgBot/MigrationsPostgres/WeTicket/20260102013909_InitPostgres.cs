using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace TgBot.MigrationsPostgres.WeTicket
{
    public partial class InitPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoughtTicket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNo = table.Column<int>(type: "integer", nullable: false),
                    FundRaiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAgent = table.Column<bool>(type: "boolean", nullable: false),
                    AgentID = table.Column<Guid>(type: "uuid", nullable: true),
                    Anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: true),
                    Time = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    PaymentCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoughtTicket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundAgent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundAgent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundAgentId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    IdInChannel = table.Column<string>(type: "text", nullable: true),
                    NameInChannel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundAgentId", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundRaiserPictureItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundRaiserID = table.Column<Guid>(type: "uuid", nullable: false),
                    PictureMIME = table.Column<string>(type: "text", nullable: true),
                    Picture = table.Column<byte[]>(type: "bytea", nullable: true),
                    ExternalStorageType = table.Column<int>(type: "integer", nullable: false),
                    IdInExternalStorage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundRaiserPictureItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundRaisingChannel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundRaisingChannel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TansferPicture",
                columns: table => new
                {
                    WithdrawalId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferPicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    TransferPictureMime = table.Column<string>(type: "text", nullable: true),
                    ExternalStorageType = table.Column<int>(type: "integer", nullable: false),
                    IdInExternalStorage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TansferPicture", x => x.WithdrawalId);
                });

            migrationBuilder.CreateTable(
                name: "TicketCompaign",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentID = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelID = table.Column<int>(type: "integer", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: true),
                    ShortDescription = table.Column<string>(type: "text", nullable: true),
                    TicketPrice = table.Column<long>(type: "bigint", nullable: false),
                    MaxTickets = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<long>(type: "bigint", nullable: false),
                    AutoCloseTime = table.Column<long>(type: "bigint", nullable: false),
                    CloseTime = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CommissionOutTenThousand = table.Column<int>(type: "integer", nullable: false),
                    RegistrationFee = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCompaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalBanks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalBanks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WithDrawalRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundRaiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<int>(type: "integer", nullable: false),
                    AccountNo = table.Column<string>(type: "text", nullable: true),
                    AccountName = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    TransferedAmount = table.Column<long>(type: "bigint", nullable: false),
                    RegistrationFee = table.Column<long>(type: "bigint", nullable: false),
                    Comission = table.Column<long>(type: "bigint", nullable: false),
                    RequestDate = table.Column<long>(type: "bigint", nullable: false),
                    RequestNote = table.Column<string>(type: "text", nullable: true),
                    ApprovedDate = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalNote = table.Column<string>(type: "text", nullable: true),
                    TransferRefernce = table.Column<string>(type: "text", nullable: true),
                    RejectDate = table.Column<long>(type: "bigint", nullable: false),
                    RejectNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithDrawalRequest", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FundRaisingChannel",
                columns: new[] { "Id", "Description" },
                values: new object[] { 1, "Telegram" });

            migrationBuilder.InsertData(
                table: "WithdrawalBanks",
                columns: new[] { "Id", "Name", "Order" },
                values: new object[] { 1, "Commercial Bank of Ethiopia", 1 });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoughtTicket");

            migrationBuilder.DropTable(
                name: "FundAgent");

            migrationBuilder.DropTable(
                name: "FundAgentId");

            migrationBuilder.DropTable(
                name: "FundRaiserPictureItem");

            migrationBuilder.DropTable(
                name: "FundRaisingChannel");

            migrationBuilder.DropTable(
                name: "TansferPicture");

            migrationBuilder.DropTable(
                name: "TicketCompaign");

            migrationBuilder.DropTable(
                name: "WithdrawalBanks");

            migrationBuilder.DropTable(
                name: "WithDrawalRequest");
        }
    }
}
