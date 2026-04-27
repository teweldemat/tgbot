using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.SmartLedger.AccountReconciliation;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    class MyActionListViewer : BotDialogBase
    {
        private const int PageSize = 5;
        private const string PagePrefix = "MY_ACTION_PAGE_";
        private const string PaymentPrefix = "MY_ACTION_PAYMENT_";
        private const string ReconciliationPrefix = "MY_ACTION_RECON_";

        public ChatId ChatId { get; set; }
        public User User { get; set; }
        public int PageIndex { get; set; }
        public int ButtonMessageId { get; set; }

        SmartLedgerService service;
        TgDbService tgService;

        public MyActionListViewer(SmartLedgerService service, TgDbService tgService, ChatId chatId, User user)
        {
            this.service = service;
            this.tgService = tgService;
            ChatId = chatId;
            User = user;
        }

        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
        }

        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            if (await ShowPageAsync(bot, 0, cancellationToken))
                return DialogResult.Handled;
            return DialogResult.Terminated;
        }

        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancellationToken)
        {
            if (callBack.Data.StartsWith(PagePrefix))
            {
                if (await ShowPageAsync(bot, PageIndex, cancellationToken))
                    return DialogResult.Handled;
                return DialogResult.Terminated;
            }

            if (callBack.Data.StartsWith(PaymentPrefix))
            {
                var paymentId = Guid.Parse(callBack.Data.Substring(PaymentPrefix.Length));
                await SetChildDialog(bot, new PaymentDetailDialog(service, tgService, ChatId, User, paymentId), cancellationToken);
                return DialogResult.Handled;
            }

            if (callBack.Data.StartsWith(ReconciliationPrefix))
            {
                var reconciliationId = Guid.Parse(callBack.Data.Substring(ReconciliationPrefix.Length));
                await SetChildDialog(bot, new ReconciliationDetailDialog(service, ChatId, User, reconciliationId), cancellationToken);
                return DialogResult.Handled;
            }

            return DialogResult.Continue;
        }

        protected override Task<DialogResult> HandleChildTerminate(ITelegramBotClient bot, Update update, IBotDialog child, CancellationToken cancellationToken)
        {
            return Task.FromResult(DialogResult.Terminated);
        }

        async Task<bool> ShowPageAsync(ITelegramBotClient bot, int index, CancellationToken cancellationToken)
        {
            var items = service.GetMyActionItems(User.Id.ToString(), index, PageSize, out var totalN);
            if (items.Count == 0)
            {
                await bot.SendTextMessageAsync(ChatId, index == 0 ? "No requests are waiting for your action." : "No more requests are waiting for your action.", cancellationToken: cancellationToken);
                return false;
            }

            var lines = new List<string>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var link = item.Kind == SmartLedgerActionKind.Payment
                    ? SmartLedgerBot.PaymentLink(item.Id, item.Reference)
                    : SmartLedgerBot.ReconciliationLink(item.Id, item.Reference);
                lines.Add($"{index + i + 1}. <b>{item.PendingAction}</b>\n{link} - {item.Description}");
            }

            var buttons = items.Select(item =>
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"Open {item.Reference}",
                        (item.Kind == SmartLedgerActionKind.Payment ? PaymentPrefix : ReconciliationPrefix) + item.Id)
                }).ToList();

            if (index + PageSize < totalN)
            {
                PageIndex = index + PageSize;
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        Program.lm.Show_more_n_records(Math.Min(PageSize, totalN - PageIndex)),
                        PagePrefix + PageIndex)
                });
            }

            if (ButtonMessageId > 0)
                await bot.EditMessageReplyMarkupAsync(ChatId, ButtonMessageId, new InlineKeyboardMarkup(new InlineKeyboardButton[0]), cancellationToken: cancellationToken);

            var message = await bot.SendTextMessageAsync(
                chatId: ChatId,
                text: "<b>My Action List</b>\n" + string.Join("\n\n", lines),
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
            ButtonMessageId = message.MessageId;
            return index + PageSize < totalN;
        }
    }
}
