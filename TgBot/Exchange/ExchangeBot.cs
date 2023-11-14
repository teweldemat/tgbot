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
using TgBot.SmartLedger;
using TgBot.SmartLedger.Dialog;
using TgBot.TgDb;

namespace TgBot.Exchange
{
    public class ExchangeBot : ITGBotApp
    {
        public static String WebLinkBaseUrl;

        const string MAIN_BUY = "Buy Crypto";
        const string MAIN_SELL = "Sell Crypto";
        const string MAIN_LIST_OFFERS = "Check Offers";
        const string MAIN_BECOME_TRUSTEE = "Become Trustee";
        const string MAIN_SETTING = "Setup";


        public string BotAppName => "Exchange";
        public Task<bool> HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
        public static Task<bool> RestartAsync(ITelegramBotClient bot, ChatId chatId, User from, String message,
            CancellationToken cancellationToken)
        {
            return ProcessStart(bot, chatId, from, message, cancellationToken);
        }
        private static async Task<bool> ProcessStart(ITelegramBotClient bot,
            ChatId chatId, User from, String message,
            CancellationToken cancellationToken)
        {
            using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
            {
                var service = serviceProvider.GetService<ExchangeDbService>();
                var prof = service.GetUserProfile(from.Id.ToString());
                if (prof == null)
                {
                    await bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Welcome");
                    await TGBot.PushDialog(from.Id.ToString(), new SetUserProfileDialog<ExchangeDb,ExchangeDbService>(service, chatId, from), cancellationToken);
                    return true;
                }

                var buttons = new List<KeyboardButton[]>();
                if (prof.UserStatus == UserStatus.Approved)
                {

                }
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
        }


        internal static async Task NotifyGroups(ITelegramBotClient bot, String message, bool html, CancellationToken cancellationToken)
        {
            try
            {
                using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
                {
                    var service = serviceProvider.GetService<TgDb.TgDbService>();
                    foreach (var g in service.GetAllJoinedTGGroups(TGBot.BotToken))
                    {
                        await bot.SendTextMessageAsync(
                                chatId: g.TgGroupId,
                                text: message,
                                parseMode: html ? ParseMode.Html : ParseMode.Default
                            );
                    }
                }
            }
            catch (Exception ex)
            {
                TGBot.LogException($"Error tring to notify groups.\n{message}", ex);
            }
        }
        internal static async Task NotifyUser(ITelegramBotClient bot, string userId, String message, CancellationToken cancellationToken)
        {
            try
            {
                await bot.SendTextMessageAsync(
                        chatId: long.Parse(userId),
                        text: message,
                        parseMode: ParseMode.Html
                    );
            }
            catch (Exception ex)
            {
                TGBot.LogException($"Error tring to notify user {userId}.\n{message}", ex);
            }
        }

        private static async Task<bool> ProcessCancel(ITelegramBotClient bot, ChatId chatId, String tgUserID, CancellationToken cancellationToken)
        {
            await TGBot.ClearDialogAsync(bot, chatId, tgUserID, cancellationToken);
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
                    using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
                    {
                        var service = serviceProvider.GetService<ExchangeDbService>();
                        var coreService = serviceProvider.GetService<TgDbService>();
                        var state = coreService.GetOrCreateUser(msg.From.Id.ToString());
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


                                //from this onward only permited users
                                var user = service.GetUserProfile(msg.From.Id.ToString());
                                if (user == null || user.UserStatus != UserStatus.Approved)
                                    return false;

                                if (msg.Text.Equals("/cancel", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    if (await ProcessCancel(botClient, msg.Chat.Id, msg.From.Id.ToString(), cancellationToken))
                                        return true;
                                }


                                switch (msg.Text)
                                {
                                    case MAIN_SETTING:
                                        return true;
                                    case MAIN_BUY:
                                        return true;
                                    case MAIN_SELL:
                                        return true;
                                    case MAIN_BECOME_TRUSTEE:
                                        return true;
                                }
                            }
                        }
                    }
                    break;
            }
            return false;
        }


        public void OnBotConnect(ITelegramBotClient botClient)
        {
            WebLinkBaseUrl = Program.GeneralConfiguration("Exchange", "Web");
        }

        public async Task<bool> HandleUpdateResidualAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message.Chat.Type == ChatType.Private && update.Message.Type == MessageType.Text)
            {
                var msg = update.Message;
                var msgTxt = msg.Text.Trim();
                await ProcessStart(botClient, msg.Chat.Id, msg.From, "Sorry, I didn't get what you are trying to do.", cancellationToken);
            }
            return false;
        }
    }
}