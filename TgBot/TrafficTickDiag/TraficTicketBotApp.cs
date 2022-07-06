using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.WFDB;
using TgBot.TrafficTickDiag;

namespace TgBot
{
    public class TraficTicketBotApp : ITGBotApp
    {
        const string MAIN_PAY = "Pay a Ticket";
        
        static WFDB.WFDBService coreService = new WFDBService();
        static WFTGDB.WFTGDBService service = new WFTGDB.WFTGDBService();
        static ITelegramBotClient theBotClient;

        public String BotAppName => "Traffic Ticket Telegram Bot";


       

        


        private static async Task<bool> ProcessCancel(ChatId chatId, String tgUserID)
        {
            await TGBot.ClearDialogAsync(chatId, tgUserID);            
            return true;
        }
        
        private static async Task<bool> ProcessStart(WFTGDB.WFTGDBService service, Message msg, TgUserState state,CancellationToken cancellation)
        {
            await TGBot.PushDialog(msg.From, new PayTicketDialog(msg.Chat.Id, msg.From), cancellation);
            return true;
        }

        private static async Task NotifyUnexpectedErroForUser(CallbackQuery q, Exception ex)
        {
            await theBotClient.SendTextMessageAsync(q.Message.Chat.Id, "Sorry! We have encountered a problem. Please contact admin");
            Console.WriteLine("Error confirming: " + ex.Message);
        }


        public Task<bool> HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
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
                    Func<TgUserState> state = () => service.GetUserState(msg.From.Id.ToString());

                    if (msg.Chat.Type == ChatType.Private)
                    {
                        if (msg.Text != null)
                        {
                            if (msg.Text.StartsWith("/start", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var parts = msg.Text.Split(' ');
                                if (parts.Length == 1)
                                {
                                    if (await ProcessStart(service, msg, state(), cancellationToken))
                                        return true;
                                }
                            }
                            if (msg.Text.Equals("/cancel", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await ProcessCancel(msg.Chat.Id, msg.From.Id.ToString()))
                                    return true;
                            }
                        }
                    }
                    break;
            }
            return false;
        }
        public void OnBotConnect(ITelegramBotClient botClient)
        {
            theBotClient = botClient;
            WFTGDB.WFTGDBService.InitializePoller();
        }
    }
}
