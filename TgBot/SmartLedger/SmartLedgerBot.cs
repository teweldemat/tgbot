using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.SmartLedger
{
    public class SmartLedgerBot : ITGBotApp
    {
        public static String WebLinkBaseUrl;
        public string BotAppName => "SmartLedger";

        public Task<bool> HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
        const string MAIN_REQUEST_PAYMENT = "Request Payment";
        const string MAIN_SHOW_ACCOUNTS = "Show Acounts";
        const string MAIN_LIST_PAYMENTS = "List Open Requests";
        const string MAIN_SETUP_FLOW = "Configure Payment System";
        const string MAIN_ADD_CASH_ACCOUNT = "Add Cash Account";
        const string MAIN_SET_USER_PROFILE = "Set User Profile";

        const string MAIN_REQUEST_DEPOSIT = "Request Deposit";
        const string MAIN_REQUEST_TRNSFER = "Request Transfer";

        public static Task<bool> RestartAsync(ITelegramBotClient bot, ChatId chatId,User from, String message,
            CancellationToken cancellationToken)
        {
            return ProcessStart(bot, chatId, from, message, cancellationToken);
        }
        public static async Task<bool> ProcessStart(ITelegramBotClient bot,
            ChatId chatId, User from, String message,
            CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();
            var prof = service.GetUserProfile(from.Id.ToString());
            if (prof == null)
            {
                await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Welcome");
                await TGBot.PushDialog(from.Id.ToString(), new SetUserProfileDialog<SmartLedgerDb>(chatId, from), cancellationToken);
                return true;
            }
            var entity = service.GetEntity();
            if (entity == null)
            {
                await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Welcome.");
                await TGBot.PushDialog(from.Id.ToString(), new SetupCompanyDialog<SmartLedgerDb>(chatId, from), cancellationToken);
                return true;
            }

            bool isOwner = entity.Owner.Equals(from.Id.ToString());
            int accountCount = service.AccountsCount();
            var buttons = new List<KeyboardButton[]>();
            var config = service.GetRule();
            var flowConfigured = config!=null && config.Rule!=null;
            if (accountCount>0 && prof.Permitted)
            {
                buttons.Add(new KeyboardButton[] { MAIN_REQUEST_PAYMENT });
                buttons.Add(new KeyboardButton[] { MAIN_REQUEST_DEPOSIT});
                if(accountCount>1)
                    buttons.Add(new KeyboardButton[] { MAIN_REQUEST_TRNSFER});
                buttons.Add(new KeyboardButton[] { MAIN_SHOW_ACCOUNTS });
                buttons.Add(new KeyboardButton[] { MAIN_LIST_PAYMENTS });
            }
            if (isOwner && prof.Permitted)
            {
                buttons.Add(new KeyboardButton[] { MAIN_SETUP_FLOW });
                if(flowConfigured)
                    buttons.Add(new KeyboardButton[] { MAIN_ADD_CASH_ACCOUNT });
            }
            buttons.Add(new KeyboardButton[] { MAIN_SET_USER_PROFILE });
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(buttons,
                    resizeKeyboard: true
                );

            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                replyMarkup: replyKeyboardMarkup
            );
            return true;
        }
        public static String PaymentLink(Guid guid,String linkText)
        {
            return $"<a href=\"{WebLinkBaseUrl}/sl/payment?id={guid}\">{linkText}</a>";
        }
        public static String GetLedgerLink(Guid accountId)
        {
            return $"{SmartLedgerBot.WebLinkBaseUrl}/sl/ledger?accountid={accountId}";
        }
        internal static async Task NotifyGroups(ITelegramBotClient bot, String message,bool html, CancellationToken cancellationToken)
        {
            foreach (var g in new TgDb.TgDbService().GetAllJoinedTGGroups(TGBot.BotToken))
            {
                await bot.SendTextMessageAsync(
                        chatId: g.TgGroupId,
                        text: message,
                        parseMode:html?ParseMode.Html:ParseMode.Default
                    );
            }
        }

        private static async Task<bool> ProcessCancel(ITelegramBotClient bot, ChatId chatId, String tgUserID,CancellationToken cancellationToken)
        {
            await TGBot.ClearDialogAsync(bot,chatId, tgUserID,cancellationToken);
            return true;
        }
       
        public async Task<bool> HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            switch (update.Type)
            {
                case UpdateType.CallbackQuery:
                    break;
                case UpdateType.Message:
                    var msg = update.Message;
                    if (msg == null
                        || msg.Chat == null
                        || msg.From == null
                        || msg.From.IsBot
                        )
                        return false;
                    var service = new SmartLedgerService();
                    var state = new TgDb.TgDbService().GetOrCreateUser(msg.From.Id.ToString());
                    if (msg.Chat.Type == ChatType.Private)
                    {
                        if (msg.Text != null)
                        {
                            if (msg.Text.StartsWith("/start", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var parts = msg.Text.Split(' ');
                                if (parts.Length == 1)
                                {
                                    if (await ProcessStart(botClient, msg.Chat.Id, msg.From, "What do you want to do?", cancellationToken))
                                        return true;
                                }
                            }
                            if(MAIN_SET_USER_PROFILE.Equals(msg.Text))
                            {
                                await TGBot.PushDialog(msg.From.Id.ToString(), new SetUserProfileDialog<SmartLedgerDb>(msg.Chat.Id, msg.From), cancellationToken);
                                return true;
                            }
                            //from this onward only permited users
                            var user = service.GetUserProfile(msg.From.Id.ToString());
                            if (user == null || !user.Permitted)
                                return false;

                            if (msg.Text.Equals("/cancel", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await ProcessCancel(botClient, msg.Chat.Id, msg.From.Id.ToString(), cancellationToken))
                                    return true;
                            }

                            /*bool inlinQuery = false;
                            foreach (var pr in new[] { Payment.PR_REF_PREFIX, Payment.DR_REF_PREFIX, Payment.TR_REF_PREFIX })
                            {
                                if (msg.Text.StartsWith(pr, StringComparison.CurrentCultureIgnoreCase) || msg.Text.StartsWith("/" + pr, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    inlinQuery = true;
                                    break;
                                }
                            }
                            if (inlinQuery)
                            {
                                string pref;
                                if (msg.Text.StartsWith("/"))
                                    pref = msg.Text.Substring(1).Trim();
                                else
                                    pref = msg.Text.Trim();
                                pref = pref.Replace(" ", "");

                                var payment = service.GetPaymentByRef(pref);
                                if (payment != null)
                                {
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new PaymentDetailDialog(msg.Chat.Id, msg.From, payment.Id), cancellationToken);
                                    return true;
                                }
                            }*/


                            switch (msg.Text)
                            {
                                case MAIN_SETUP_FLOW:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new SetupFlowDialog(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                                case MAIN_ADD_CASH_ACCOUNT:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new AddCashAccountDialog(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                                case MAIN_SET_USER_PROFILE:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new SetUserProfileDialog<SmartLedgerDb>(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                                case MAIN_REQUEST_PAYMENT:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new RequestPaymentDialog(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                                case MAIN_REQUEST_DEPOSIT:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new RequestPaymentDialog(msg.Chat.Id, msg.From, paymentType: PaymentType.Deposit), cancellationToken);
                                    return true;
                                case MAIN_REQUEST_TRNSFER:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new RequestPaymentDialog(msg.Chat.Id, msg.From, paymentType: PaymentType.Transfer), cancellationToken);
                                    return true;
                                case MAIN_LIST_PAYMENTS:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new PaymentListViewer(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                                case MAIN_SHOW_ACCOUNTS:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new AccountListViewer(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                            }
                        }

                    }
                    break;
            }
            return false;
        }
        public static String FormatDetailHtml(Guid id)
        {

            var service = new SmartLedgerService();
            var p = service.GetPayment(id);
            var statusString = $"{(p.HeadType == null ? "Unknown" : PaymentWorkItem.StatusString(p.HeadType.Value,p))}";
            var text =
                p.IsDeposit
                ?$"Reference: {p.Reference}"
                + $"<pre>\n</pre>Amount: {IntData.toString(p.PositiveAmount)}"
                + $"<pre>\n</pre>Paid From:{p.ToPayTo}"
                + $"<pre>\n</pre>Paid for: {p.Note}"
                + $"<pre>\n</pre>Status: {statusString}"

                :$"Reference: {p.Reference}"
                + $"<pre>\n</pre>Amount: {IntData.toString(p.PositiveAmount)}"
                + $"<pre>\n</pre>To:{p.ToPayTo}"
                + $"<pre>\n</pre>Purpose: {p.Note}"
                + $"<pre>\n</pre>Status: {statusString}"
                ;
            if (p.WorkItemHead != null)
            {
                var w = service.GetWorkItem(p.WorkItemHead.Value);
                var userState = service.GetUserProfile(w.UserId);
                switch (w.WorkType)
                {
                    case PaymentWorkItem.WORK_TYPE_CREATE:
                        text += $"<pre>\n</pre>Requested By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_CHECK:
                        text += $"<pre>\n</pre>Checked By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_APPROVE:
                        text += $"<pre>\n</pre>Approved By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_ACCOUNT:
                        text += $"<pre>\n</pre>Accounted By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_CLOSE:
                        text += $"<pre>\n</pre>Closed By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_PAY:
                        if (p.IsDeposit)
                            text += $"<pre>\n</pre>Received By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        else if (p.IsTransferTransaction)
                            text += $"<pre>\n</pre>Trasnfered By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        else
                            text += $"<pre>\n</pre>Paid By:{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_CANT_PAY:
                        if (p.IsDeposit)
                            text += $"<pre>\n</pre>Deposit declined by :{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        else if (p.IsTransferTransaction)
                            text += $"<pre>\n</pre>Transfer declined by :{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        else
                            text += $"<pre>\n</pre>Payment declined by :{userState.FullName} on {IntData.toDateString(w.Time, "MMM dd,yy")}";
                        break;
                }
                text += $"<pre>\n</pre>Remark: {w.Note}";
            }
            text += $"<pre>\n</pre>{SmartLedgerBot.PaymentLink(p.Id,p.Reference)}";
            return text;
        }

        internal static Task NotifyGroups(ITelegramBotClient bot, object p, bool v, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void OnBotConnect(ITelegramBotClient botClient)
        {
            WebLinkBaseUrl = Program.GeneralConfiguration("Smartledger","Web");
        }

        public async Task<bool> HandleUpdateResidualAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            if (update.Type == UpdateType.Message && update.Message.Chat.Type == ChatType.Private && update.Message.Type == MessageType.Text)
            {
                var msg = update.Message;
                var msgTxt = msg.Text.Trim();
                bool taxCodeQuery = false;
                foreach (var pr in new[] { Payment.PR_REF_PREFIX,Payment.DR_REF_PREFIX,Payment.TR_REF_PREFIX })
                {
                    if (msgTxt.StartsWith(pr, StringComparison.CurrentCultureIgnoreCase) ||
                        msgTxt.StartsWith("/" + pr, StringComparison.CurrentCultureIgnoreCase))
                    {
                        taxCodeQuery = true;
                        break;
                    }
                }
                if (taxCodeQuery)
                {
                    string taskCode;
                    if (msgTxt.StartsWith("/"))
                        taskCode = msgTxt.Substring(1).Trim();
                    else
                        taskCode = msgTxt.Trim();
                    taskCode = taskCode.Replace(" ", "");
                    var service = new SmartLedgerService();
                    var payment = service.GetPaymentByRef(taskCode);
                    if (payment != null)
                    {
                        await TGBot.PushDialog(msg.From.Id.ToString(), new PaymentDetailDialog(msg.Chat.Id, msg.From, payment.Id), cancellationToken);
                        return true;
                    }
                }
                if (msgTxt.Length > 3)
                {
                    await TGBot.PushDialog(msg.From.Id.ToString(), new PaymentListViewer(msg.Chat.Id, msg.From, false, filterText: msgTxt), cancellationToken);
                    return true;
                }
                await ProcessStart(botClient, msg.Chat.Id, msg.From, "Sorry, I don't understand what you are trying to say.\nAs a bot, I can sometimes be dumb.", cancellationToken);
            }
            return false;
        }
    }
}
