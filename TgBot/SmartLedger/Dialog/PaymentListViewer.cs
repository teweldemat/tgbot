using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.SmartLedger.Dialog
{
    class PaymentListViewer : BotDialogBase
    {
        private const int CONTLIST_PAGE_SIZE = 5;
        private const string PAGE_PREFIX = "PAGE_";
        public ChatId chatId;
        public User user;
        public int pageIndex;
        public int buttonMsgId;
        public string prevText;
        public bool ActiveOnly;
        public string filterText = null;
        SmartLedgerService service;

        public PaymentListViewer(SmartLedgerService service, ChatId chatId, User user
            , bool activeOnly = true, string filterText = null
            )
        {
            this.chatId = chatId;
            this.user = user;
            ActiveOnly = activeOnly;
            this.filterText = filterText;
            this.service = service;

        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
        }
        public static string FormatPayment(SmartLedgerService coreService, Payment payment, string numLabel)
        {
            string name = coreService.GetUserProfile(payment.Creator).FullName;

            var html = $"{numLabel}";
            if (payment.IsDeposit)
                html += $"Recieve {IntData.toString(payment.PositiveAmount)} Birr from {payment.ToPayTo} for {payment.Note} /{payment.Reference}";
            else if (payment.IsTransferTransaction)
                html += $"Transfer {IntData.toString(payment.PositiveAmount)} Birr to {coreService.GetCashAccount(payment.TransferTo.Value).Name} for {payment.Note} /{payment.Reference}";
            else
                html += $"Pay {IntData.toString(payment.PositiveAmount)} Birr to {payment.ToPayTo} for {payment.Note} /{payment.Reference}";
            return html;
        }
        async Task<bool> ShowPageAsync(ITelegramBotClient bot, int index, CancellationToken cancelationToken)
        {
            var payments = service.GetOpenPayments(index, CONTLIST_PAGE_SIZE, out var totalN, textFilter: filterText);
            if (payments.Count == 0)
            {
                await bot.SendTextMessageAsync(chatId, "No open request");
            }
            else
            {
                var n = 1;
                string listHtml = null;
                foreach (var f in payments)
                {
                    var html = FormatPayment(service, f, $"{index + n}. ");
                    listHtml = listHtml == null ? html : listHtml + "\n" + html;
                    n++;
                }
                listHtml += $"\n<a href=\"{SmartLedgerBot.WebLinkBaseUrl}/sl/summary\">[Full Summary]</a>";
                if (index > 0)
                {
                    await bot.EditMessageReplyMarkupAsync(chatId, buttonMsgId, new InlineKeyboardMarkup(new InlineKeyboardButton[0]));
                }
                if (index + CONTLIST_PAGE_SIZE < totalN)
                {
                    pageIndex = index + CONTLIST_PAGE_SIZE;
                    var buttons = new InlineKeyboardButton[][]
                        {
                        new[] { InlineKeyboardButton.WithCallbackData(Program.lm.Show_more_n_records(Math.Min(CONTLIST_PAGE_SIZE, totalN - (index + CONTLIST_PAGE_SIZE))), PAGE_PREFIX + pageIndex), }
                        };
                    var replyKeyboardMarkup = new InlineKeyboardMarkup(buttons.ToArray());
                    var msg = await bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: listHtml,
                        parseMode: ParseMode.Html,
                        replyMarkup: replyKeyboardMarkup
                    );
                    buttonMsgId = msg.MessageId;
                    prevText = listHtml;
                }
                else
                {
                    await bot.SendTextMessageAsync(chatId,
                        text: listHtml,
                        parseMode: ParseMode.Html);
                }

            }
            return totalN > index + CONTLIST_PAGE_SIZE;

        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            if (await ShowPageAsync(bot, 0, cancelationToken))
                return DialogResult.Handled;
            return DialogResult.Terminated;
        }

        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            if (callBack.Data.IndexOf(PAGE_PREFIX) == 0)
            {
                if (await ShowPageAsync(bot, pageIndex, cancelationToken))
                    return DialogResult.Handled;
                return DialogResult.Terminated;
            }

            return DialogResult.Continue;
        }
    }
}
