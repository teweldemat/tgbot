using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.TrafficTickDiag
{
    public class PayTicketDialog : BotDialogBase
    {
        const string DONE = "PayTicketDone";
        const string RESEND_SMS = "PayTicketResendSMS";


        const string YES= "PayTicketYes";
        const string NO= "PayTicketNo";

        const string FIELD_RANK = "FieldRank";
        const string FIELD_DRIVING_LICENSE_NO = "FieldDrivingLicenseNo";
        const string FIELD_CAR_LICENSE_NO = "FieldCarLicenseNo";
        const String FIELD_TRAFIC_POLICE_NAME = "FieldPoliceName";
        const String FIELD_TRAFIC_PHONE_NO = "FieldPhoneNo";

        public ChatId chatId;
        public User from;
        public String selectedField;
        public string checkOutCode;
        public bool paid = false;
        
        public TTDB.Ticket ticket;
        public String policePhoneNo;
        public PayTicketDialog(ChatId chatId, User from)
        {
            this.chatId = chatId;
            this.from = from;
            this.ticket = new TTDB.Ticket();
        }
        
        public override async Task<DialogResult> HandlePaymentAsync(ITelegramBotClient bot, string checkOutCode, CancellationToken cancelationToken)
        {
            if (paid)
                return DialogResult.Continue;
            if (checkOutCode == null || this.checkOutCode == null || !checkOutCode.Replace(" ", "").Equals(this.checkOutCode.Replace(" ", "")))
                return DialogResult.Continue;
            var service = new WFTGDB.WFTGDBService();
            var coreService = new TTDB.TTDBService();
            try
            {
                paid = true;
                try
                {
                    coreService.PayTicket(this.ticket.Id);
                    var finishButtons = new InlineKeyboardMarkup(
                                            new[]{new []{
                                                    InlineKeyboardButton.WithCallbackData("Done",DONE),
                                                    InlineKeyboardButton.WithCallbackData("Resend SMS",RESEND_SMS),
                                                }});
                    await bot.SendTextMessageAsync(chatId, "Thank you. We have received the payment.\nWe are sending the receipt to the traffic police person phone no.", replyMarkup: finishButtons);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error trying to send thank you message after payment is made.\n{ex.Message}\n{ex.StackTrace}");
                }

                await SendReceiptSMS();
                return DialogResult.Handled;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error after payment: {ex.Message}");
                return DialogResult.Handled;
            }
        }

        private async Task SendReceiptSMS()
        {
            try
            {
                var res = await (new SMSService()).SendMessageAsync(
                    $@"Penality paid
License No: {ticket.DrivingLicenseNo}
Plate No: {ticket.PlateNo}
Penality Rank: {ticket.PenalityRankNo}
Payment Receipt: {ticket.ReceiptNo}
Payment Amount: {IntData.toString(ticket.Amount)}",
                    policePhoneNo);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to SMS the police", ex);
            }
        }

        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await bot.SendTextMessageAsync(chatId, "What is the rank of your traffic violation?");
            selectedField = FIELD_RANK; ;
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery q, CancellationToken  cancellationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            switch (q.Data)
            {
                case YES:
                    await bot.SendTextMessageAsync(
                        chatId: q.Message.Chat.Id,
                        text: "We are preparing checkout code for your payment"
                    );
                    await WaitForPaymentAsync(bot, q.From);
                    return DialogResult.Handled;
                case DONE:
                    await bot.SendTextMessageAsync(q.Message.Chat.Id, "Thanks, goodbye.");
                    return DialogResult.Terminated;
                case NO:
                    await bot.SendTextMessageAsync(q.Message.Chat.Id, "Alright, goodbye.");
                    return DialogResult.Terminated;
                case RESEND_SMS:
                    await bot.SendTextMessageAsync(q.Message.Chat.Id, "Ok, we are trying to resend the SMS. Sometimes the SMS service can be down");
                    await SendReceiptSMS();
                    return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }
        
        async Task WaitForPaymentAsync(ITelegramBotClient bot, User u)
        {

            this.checkOutCode= await WFTGDB.WFTGDBService.WBCheckOutAPI.CheckOut(TGBot.FullName(u), u.Username, $"Traffic Penality Payment", ticket.Amount);
            new WFTGDB.WFTGDBService().SetStateData(u.Id.ToString(), data =>
            {
                data.WaitingForPayment = true;
                data.WbcCode = this.checkOutCode;
            });
            WFTGDB.WFTGDBService.ResumePaymentPoll();
            var html = $"Use the following Code to pay with WeBirr";
            await bot.SendTextMessageAsync(chatId, html, ParseMode.Html);
            await bot.SendTextMessageAsync(chatId, $"<strong>{checkOutCode}</strong>", ParseMode.Html);
            await bot.SendTextMessageAsync(chatId, $"<a href=\"{Program.GetBotConfig("InstPage").Replace("{wbc_checkout}", checkOutCode)}\">How to pay with WeBirr?</a>", ParseMode.Html);
        }

        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message msg, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            var corservice = new TTDB.TTDBService();
            switch (selectedField)
            {
                case FIELD_RANK:
                    var er = TGBot.GetIntParseError(msg.Text, x=>corservice.CalculateAmount(x)==-1?"Invalid rankd":null, out int rank);
                    if (er!=null)
                        await bot.SendTextMessageAsync(msg.Chat.Id, er);
                    else
                    {
                        ticket.PenalityRankNo = rank;
                        ticket.Amount = corservice.CalculateAmount(rank);
                        await bot.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Enter your license no"
                        );
                        selectedField = FIELD_DRIVING_LICENSE_NO;
                    }
                    return DialogResult.Handled;
                case FIELD_DRIVING_LICENSE_NO:
                    ticket.DrivingLicenseNo = msg.Text;
                    await bot.SendTextMessageAsync(
                        chatId: msg.Chat.Id,
                        text: "Enter your car plate no"
                    );
                    selectedField = FIELD_CAR_LICENSE_NO;
                    return DialogResult.Handled;
                case FIELD_CAR_LICENSE_NO:
                    ticket.PlateNo = msg.Text;
                    await bot.SendTextMessageAsync(
                        chatId: msg.Chat.Id,
                        text: "Enter the name of the traffic police person"
                    );
                    selectedField = FIELD_TRAFIC_POLICE_NAME;
                    return DialogResult.Handled;
                case FIELD_TRAFIC_POLICE_NAME:
                    ticket.PoliceName = msg.Text;
                    await bot.SendTextMessageAsync(
                        chatId: msg.Chat.Id,
                        text: "Enter the phone no of the traffic police person to send receipt to"
                    );
                    selectedField = FIELD_TRAFIC_PHONE_NO;
                    return DialogResult.Handled;
                case FIELD_TRAFIC_PHONE_NO:
                    this.policePhoneNo = msg.Text;
                    this.ticket = new TTDB.TTDBService().RegisterTicket(ticket.PenalityRankNo, ticket.DrivingLicenseNo, ticket.PlateNo);

                    var finishButtons = new InlineKeyboardMarkup(
                        new[]{new []{
                                                    InlineKeyboardButton.WithCallbackData("Yes, it looks good",YES),
                                                    InlineKeyboardButton.WithCallbackData("No, cancel",NO),
                                                }});
                    await bot.SendTextMessageAsync(chatId, 
$@"License No: {ticket.DrivingLicenseNo}
Plate No: { ticket.PlateNo}
Penality Rank: { ticket.PenalityRankNo}
Payment Receipt: { ticket.ReceiptNo}
Payment Amount: { IntData.toString(ticket.Amount)}
Police Phone No.: { this.policePhoneNo}
Do you want to continue?", 
                        replyMarkup: finishButtons);
                    return DialogResult.Handled;


            }
            return DialogResult.Continue;
        }
    }
}
