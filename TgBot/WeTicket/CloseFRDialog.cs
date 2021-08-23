using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot
{
    public class CloseFRDialog:BotDialogBase
    {
        const String YES = "YesCloseFR";
        const String NO = "No";

        public User from;
        public ChatId chatId;
        public Guid frid;
        public CloseFRDialog(ITelegramBotClient bot, ChatId chatId, User from,Guid frid)
        {
            this.chatId = chatId;
            this.from = from;
            this.frid = frid;
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            var service= new WFTGDB.WFTGDBService();
            var state = service.GetUserState(from.Id.ToString());
            var replyKeyboardMarkup = new InlineKeyboardMarkup(
                                    new[]{new []{
                                        InlineKeyboardButton.WithCallbackData("Yes, I want to close.",YES)
                                    },
                                    new []{
                                        InlineKeyboardButton.WithCallbackData("No.",NO)
                                    },
                                    }
                                );
            await bot.SendTextMessageAsync(chatId,
                text: "Are you sure you want to close this WeFund?",
                replyMarkup: replyKeyboardMarkup,
                parseMode: ParseMode.Html);
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            switch (callBack.Data)
            {
                case YES:
                    var service = new WFDB.WFDBService();
                    var user = service.GetAgentByChannel(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, callBack.From.Id.ToString());
                    if (user == null)
                        throw new UserFriendlyError("You are not allowed to close this WeFund");
                    service.CloseFundRaider(user.Id, frid);
                    await bot.SendTextMessageAsync(chatId,
                        text: "Your WeFund is closed"
                        );
                    return DialogResult.Terminated;
                case NO:
                    await bot.SendTextMessageAsync(chatId,
                        text: "Ok, the WeFund remains"
                        );
                    return DialogResult.Terminated;
            }
            return DialogResult.Continue;
        }
    }
}
