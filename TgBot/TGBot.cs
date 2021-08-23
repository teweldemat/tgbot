using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot
{
    public class TGBot : IUpdateHandler
    {
        private const string SYS_CMD_SET_NOW = "/set_now";
        private const string SYS_CMD_GET_NOW = "/get_now";
        private const string SYS_CMD_GET_OFFS = "/get_offset";
        public static TelegramBotClient Bot;
        public static String BotToken;
        public UpdateType[] AllowedUpdates => new UpdateType[] { UpdateType.CallbackQuery,
        UpdateType.ChannelPost,
        UpdateType.ChatMember,
        UpdateType.ChosenInlineResult,
        UpdateType.EditedChannelPost,
        UpdateType.EditedMessage,
        UpdateType.InlineQuery,
        UpdateType.Message,
        UpdateType.MyChatMember,
        UpdateType.Poll,
        UpdateType.PollAnswer,
        UpdateType.PreCheckoutQuery,
        UpdateType.ShippingQuery,
        UpdateType.Unknown
        };

        public static String BotAdmin;
        static TgDb.TgDbService service = new TgDb.TgDbService();
        public static String meName;
        static ITGBotApp botApp = null;
        static long TimeOffset;
        static bool DebugMode;
        static TGBot()
        {
            var typeStr = Program.GetBotConfig("BotApp");
            if (String.IsNullOrEmpty(typeStr))
                throw new Exception($"BotApp configuration is not set");
            var t = Type.GetType(typeStr);
            if (t == null)
                throw new Exception($"BotApp type {typeStr} is not valid");
            DebugMode = bool.Parse(Program.GetBotConfig("DebugMode"));
            if (DebugMode)
                TimeOffset = long.Parse(Program.GetBotConfig("TimeOffset"));
            var o = Activator.CreateInstance(t);
            botApp = o as ITGBotApp;
            if (botApp == null)
                throw new Exception($"BotApp type {typeStr} doesn't implement ITGBotApp");
        }
        public TGBot()
        {
            
            
        }
        public async static void StartAsync()
        {
            BotAdmin = Program.GetBotConfig("Admin");
            BotToken = Program.GetBotConfig("BotToken");
            Bot = new TelegramBotClient(BotToken);
            Console.WriteLine($"Trying to connect bot @{BotToken}");
            var me = await Bot.GetMeAsync();
            Console.Title = me.Username;
            meName = me.Username;
            
            Bot.StartReceiving(new TGBot());
            botApp.OnBotConnect(Bot);
            Console.WriteLine($"Start listening for @{me.Username}");
        }
        public static bool IsBotAdmin(User user)
        {
            return BotAdmin.Equals(user.Username);
        }
        public static String ToTimeLengthStr(long dt)
        {
            var ts = new TimeSpan(dt);
            if (ts.TotalMinutes < 1.01)
                return "Less than a minute";
            if (ts.TotalHours < 1)
                return $"{(int)Math.Round(ts.TotalMinutes)} Minutes";
            if (ts.TotalDays < 1)
            {
                int min = (int)Math.Round(ts.TotalMinutes);
                return $"{min/60} Hours and {min%60} Minutes";
            }
            return $"{(int)Math.Round(ts.TotalDays)} days";
        }
        internal static String ToRelativeTime(long time)
        {
            var now = NowDt();
            var dt = new DateTime(time);
            if (Math.Abs(now.Subtract(dt).TotalSeconds) < 60)
                return "now";
            if(now.Year==dt.Year)
            {
                if (now.Month == dt.Month && now.Day == dt.Day)
                {
                        return dt.ToString("HH:mm");
                }
                if (dt.Date == dt)
                    return dt.ToString("MMM dd");
                return dt.ToString("MMM dd, HH:mm");
            }
            return dt.ToString("MMM dd,yyy, HH:mm");
        }

        public static async Task<bool> HandleDialog(ITelegramBotClient botClient, Update update, String checkOutCode, String checkOutUserId, CancellationToken cancelationToken)
        {
            string userId;
            if (update == null)
            {
                userId = checkOutUserId;
            }
            else
            {
                switch (update.Type)
                {
                    case UpdateType.CallbackQuery:
                        userId = update.CallbackQuery.From.Id.ToString();
                        break;
                    case UpdateType.Message:
                        userId = update.Message.From.Id.ToString();
                        break;
                    default:
                        return false;
                }
            }
            var state = service.GetUserState(userId);
            WFDialogItem stack;
            var id = state.StackHead;
            while (id != null)
            {
                stack = service.GetDialogItem(userId, id.Value);
                var diag = stack.Deserialize();
                DialogResult res;
                if (update == null)
                    res = await diag.HandlePaymentAsync(botClient, checkOutCode, cancelationToken);
                else
                    res = await diag.HandleUpdateAsync(botClient, update, cancelationToken);

                service.SaveDialogState(stack, diag);
                if (res == DialogResult.Terminated)
                {
                    service.RemoveDialogItem(id.Value);
                    return true;
                }
                if (res == DialogResult.Handled)
                {
                    return true;
                }
                id = stack.Next;
            }
            return false;
        }
        public async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            Chat defaultChat = null;
            try
            {
                switch (update.Type)
                {
                    case UpdateType.MyChatMember:
                        if (update.MyChatMember.Chat.Type == ChatType.Group || update.MyChatMember.Chat.Type == ChatType.Supergroup)
                        {
                            if (update.MyChatMember.NewChatMember.Status == ChatMemberStatus.Left)
                                service.LeftGroup(TGBot.BotToken, update.MyChatMember.Chat.Id);
                            else
                                service.JoinedGroup(TGBot.BotToken, update.MyChatMember.Chat.Id);
                            return;
                        }
                        break;
                    case UpdateType.ChatMember:
                        if (update.ChatMember.Chat.Type == ChatType.Group || update.ChatMember.Chat.Type == ChatType.Supergroup)
                        {
                            service.JoinedGroup(TGBot.BotToken, update.ChatMember.Chat.Id);
                            return;
                        }
                        break;
                    case UpdateType.CallbackQuery:
                        defaultChat = update.CallbackQuery.Message.Chat;
                        if (update.CallbackQuery == null
                            || update.CallbackQuery.From == null
                            || update.CallbackQuery.Data == null
                            )
                            return;
                        var q = update.CallbackQuery;
                        await Bot.AnswerCallbackQueryAsync(
                            callbackQueryId: q.Id,
                            text: $"Received {q.Data}"
                        );
                        break;
                    case UpdateType.Message:
                        defaultChat = update.Message.Chat;
                        break;
                }
                if (await ProcessSystemLevel(botClient, update, cancellationToken))
                    return;
                if (await botApp.HandleUpdateAsync(botClient, update, cancellationToken))
                    return;
                if (await HandleDialog(botClient, update, null, null, cancellationToken))
                    return;
                if (await botApp.HandleUpdateResidualAsync(botClient, update, cancellationToken))
                    return;

            }
            catch (UserFriendlyError uer)
            {
                LogException("Error processing update.",uer);
                Console.WriteLine($": {uer.Message}");
                if (defaultChat != null)
                    await Bot.SendTextMessageAsync(defaultChat.Id, uer.Message);
            }
            catch (Exception ex)
            {
                LogException("Error processing update.", ex);
                if (defaultChat != null)
                    await Bot.SendTextMessageAsync(defaultChat.Id, "Sorry, but there was a system problem. Try again later.");
            }
        }

        

        private async Task<bool> ProcessSystemLevel(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if(update.Type==UpdateType.Message && !String.IsNullOrEmpty(update.Message.Text))
            {
                var cmd = update.Message.Text;
                if(TGBot.DebugMode && cmd.StartsWith(SYS_CMD_SET_NOW,StringComparison.OrdinalIgnoreCase))
                {
                    if(DateTime.TryParse(cmd.Substring(SYS_CMD_SET_NOW.Length),out var dt))
                    {
                        //var utc = TimeZoneInfo.ConvertTimeToUtc(dt);
                        TimeOffset = dt.Ticks - DateTime.Now.Ticks;
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Time offset by {new TimeSpan(TimeOffset).TotalMinutes} minutes");
                        return true;
                    }
                }
                if (cmd.StartsWith(SYS_CMD_GET_NOW, StringComparison.OrdinalIgnoreCase))
                {
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Curren time is {TGBot.NowDt()}");
                    return true;
                }
            }
            return false;
        }
        public static long Now()
        {
            if (DebugMode)
                return DateTime.Now.Ticks + TimeOffset;
            return DateTime.Now.Ticks;
        }
        public static DateTime NowDt()
        {
            if (DebugMode)
                return new DateTime(DateTime.Now.Ticks + TimeOffset);
            return DateTime.Now;
        }
        public static void LogException(String msg,Exception ex)
        {
            int n = 1;
            Console.WriteLine(msg);
            while(ex!=null)
            {
                Console.WriteLine($"Error {n}. {ex.Message}\n{ex.StackTrace}");
                ex = ex.InnerException;
                n++;
            }
        }
        public static String FullName(User user)
        {
            String name = null;
            if (!String.IsNullOrEmpty(user.FirstName))
                name = user.FirstName;
            if (!String.IsNullOrEmpty(user.LastName))
                if (name == null)
                    name = user.LastName;
                else
                    name = name + " " + user.LastName;
            return name;
        }
        public async Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            await botApp.HandleErrorAsync(botClient, exception, cancellationToken);
            LogException("Exception from bot client", exception);
        }
        public static async Task PushDialog(String userId, IBotDialog d, CancellationToken cancelationToken)
        {
            var res = await d.StartAsync(Bot, cancelationToken);
            if (res == DialogResult.Terminated)
                return;
            var state = service.GetOrCreateUser(userId);
            if (state.StackHead != null)
            {
                var diag = service.GetDialogItem(userId, state.StackHead.Value).Deserialize();
                if (diag != null)
                {
                    if (diag.OverlapPolicy == DialogOverlapPolicy.CancelOnOverlap)
                    {
                        if (await diag.HandleCancel(Bot, cancelationToken) != DialogResult.Terminated)
                            throw new Exception("Dialog should terminate on call to cancel");

                    }
                    else
                        throw new Exception("Only cancel on overlap allowed");
                }
                service.RemoveDialogItem(state.StackHead.Value);
            }
            service.PushDialog(userId, d);
        }

        internal static bool DialogActive(string tgUserID)
        {
            var state=service.GetUserState(tgUserID);
            if (state == null || state.StackHead == null)
                return false;
            return true;
        }
        internal static WFDialogItem CurrentDialog(string tgUserID)
        {
            var state = service.GetUserState(tgUserID);
            if (state == null || state.StackHead == null)
                return null;
            return service.GetDialogItem(tgUserID,state.StackHead.Value);
        }
        internal static async Task ClearDialogAsync(ITelegramBotClient bot, ChatId chatId, string tgUserID,CancellationToken cancellationToken)
        {
            var state = service.GetUserState(tgUserID);
            var diag = state.StackHead == null ? null : service.GetDialogItem(tgUserID,state.StackHead.Value);
            while(diag!=null)
            {
                try
                {
                    var d = diag.Deserialize();
                    if(d!=null)
                        await d.HandleCancel(bot, cancellationToken);
                }
                catch(Exception ex)
                {
                    TGBot.LogException($"Error trying to cancel dialog {diag.id}", ex);
                }
                diag = diag.Next== null ? null : service.GetDialogItem(tgUserID, diag.Next.Value);
            }
            int count = service.ClearDialogStack(tgUserID);
            if (count == 0)
            {
                await Bot.SendTextMessageAsync(chatId, Program.lm.Nothing_to_cancel);
            }
            else
            {
                await Bot.SendTextMessageAsync(chatId, Program.lm.Ok_canceled);
            }
        }
        public static String GetParseError(string txt,Func<double,String> amountValidate, out double amount)
        {
            amount = 0;
            if (txt.Contains("cent", StringComparison.CurrentCultureIgnoreCase))
                return "Don't use cents";
            var sanitized = txt.Replace("Birrs", "",StringComparison.CurrentCultureIgnoreCase)
                .Replace("Birr", "", StringComparison.CurrentCultureIgnoreCase);
            if (double.TryParse(sanitized, out amount))
            {
                return amountValidate(amount);
            }
            return "This doesn't look right. Enter again correctly.";
        }
        public static String GetIntParseError(string txt, Func<int, String> amountValidate, out int amount)
        {
            amount = 0;
            if (txt.Contains("cent", StringComparison.CurrentCultureIgnoreCase))
                return "Don't use cents";
            var sanitized = txt.Replace("Birrs", "", StringComparison.CurrentCultureIgnoreCase)
                .Replace("Birr", "", StringComparison.CurrentCultureIgnoreCase);
            if (int.TryParse(sanitized, out amount))
            {
                return amountValidate(amount);
            }
            return "This doesn't look right. Enter again correctly.";
        }

        internal static String MimeToExtension(string imageMime)
        {
            try
            {
                return MimeTypes.MimeTypeMap.GetExtension(imageMime);
            }
            catch
            {
                switch(imageMime.ToLower())
                {
                    case "application/sql":
                        return ".sql";
                    default:
                        throw new Exception("Unsupported content type");
                }
            }
        }
    }
}
