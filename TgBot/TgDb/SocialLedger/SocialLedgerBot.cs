using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.SocialLedger
{
    public class SocialLedgerBot : ITGBotApp
    {
        public static String WebLinkBaseUrl;
        public string BotAppName => "SmartLedger";

        public Task<bool> HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
        const string MAIN_REQUEST_FRIEND = "Ledger Friend Request";
        const string MAIN_SHOW_FRIENDS = "Show Ledgers";
        const string MAIN_RECORD_LOAN = "Record Loan";

        private static async Task<bool> ProcessStart(ITelegramBotClient bot,
            ChatId chatId, User from, String message,
            CancellationToken cancellationToken)
        {
            var service = new SocialLedgerDbService();
            var buttons = new List<KeyboardButton[]>();
            buttons.Add(new KeyboardButton[] { MAIN_REQUEST_FRIEND });
            buttons.Add(new KeyboardButton[] { MAIN_SHOW_FRIENDS });
            buttons.Add(new KeyboardButton[] { MAIN_RECORD_LOAN});
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(buttons,
            resizeKeyboard: true
            );

            await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "What do you want to do?",
                replyMarkup: replyKeyboardMarkup
            );
            return true;
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
                                else if (parts.Length == 2)
                                {
                                    if (Guid.TryParse(parts[1],out var requestId))
                                    {
                                        await TGBot.PushDialog(msg.From.Id.ToString(), new ProcessRequestDialog(msg.Chat.Id, msg.From,
                                            requestId), cancellationToken);
                                        return true;
                                    }
                                }
                            }
                            if (msg.Text.Equals("/cancel", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await ProcessCancel(botClient,msg.Chat.Id, msg.From.Id.ToString(), cancellationToken))
                                    return true;
                            }

                            switch (msg.Text)
                            {
                                case MAIN_RECORD_LOAN:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new RecordLoanDialog(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                                case MAIN_REQUEST_FRIEND:
                                    var service = new SocialLedgerDbService();
                                    var request= service.CreateRequest(msg.From.Id.ToString(), TGBot.FullName(msg.From));
                                    var buttons = new InlineKeyboardButton[] []{
                                        new[] { InlineKeyboardButton.WithSwitchInlineQuery("Send to Friend",
                                        $"https://telegram.me/{TGBot.meName}?start={request}"
                                        )}};
                                    await botClient.SendTextMessageAsync(msg.Chat.Id, "Request Created", 
                                        replyMarkup: new InlineKeyboardMarkup(buttons));
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
            var service = new SocialLedgerDbService();
            var p = service.GetLedgerPair(id);
            return "The ledger";
        }

        internal static Task NotifyGroups(ITelegramBotClient bot, object p, bool v, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void OnBotConnect(ITelegramBotClient botClient)
        {
            WebLinkBaseUrl = Program.GeneralConfiguration("SocialLedger","Web");
        }
        public Task<bool> HandleUpdateResidualAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

    }
}
