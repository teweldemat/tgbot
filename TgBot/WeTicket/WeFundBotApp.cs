using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.WFDB;
using TgBot.Dialogs;

namespace TgBot
{
    public class WeFundBotApp : ITGBotApp
    {
        const string MAIN_START_FR = "Start a new fundraiser";
        const string MAIN_LIST_FR = "List fundraiser";
        const string MAIN_LIST_MY_FR = "List my fundraiser";
        const string MAIN_LIST_WR_REQ = "List withdrwal requests";


        const string INLINE_START_CONTRIBUTE = "StartContribute";
        const string INLINE_START_DETAILS = "StartDetails";
        const string INLINE_PROCESS_TRANSER = "ProcessTransfer";

        const string INLINE_FR_START_CLOSE = "FRStartClose";
        const string INLINE_FR_START_UPDATE = "FRStartUpdate";
        const string INLINE_FR_START_TRANSFER_REQUEST = "FRStartTransferRequest";
        const string INLINE_FR_LIST_CONT = "FRListCont";

        public const int MAX_PIC_BYTES = 5000000;
        private const string JOIN_GROUP_PREFIX = "Join";
        
        static WFDB.WFDBService coreService = new WFDBService();
        static WFTGDB.WFTGDBService service = new WFTGDB.WFTGDBService();
        static ITelegramBotClient theBotClient;

        public String BotAppName => "WeFund Telegram Bot";
        delegate Task<bool> ProcessSelectedFRDelegate(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken);
        private async Task<bool> ProcessSelectedFR(User from, ChatId chatId, String data, bool showDetail, CancellationToken cancelationToken)
        {
            var method = new Tuple<String, ProcessSelectedFRDelegate>[]
            {
                new (INLINE_START_CONTRIBUTE,ProcessContribute),
                new (INLINE_START_DETAILS,ProcessShowDetailPrivate),
                new (INLINE_FR_START_CLOSE,ProcessStartFrClose),
                new (INLINE_FR_START_UPDATE,ProcessStartFrUpdate),
                new (INLINE_FR_START_TRANSFER_REQUEST,ProcessStartFrTransfer),
                new (INLINE_FR_LIST_CONT,DisplayContributions),
                new (INLINE_START_CONTRIBUTE,ProcessContribute),
                new (INLINE_PROCESS_TRANSER,ProcessProcessTransfer),
            };
            foreach (var t in method)
            {
                if (data.StartsWith(t.Item1))
                {
                    var frid = Guid.Parse(data.Substring(t.Item1.Length));
                    if (showDetail)
                        await DisplayDetailAsync(coreService.GetFundRaiser(frid), chatId, from, null, null, false);
                    if (await t.Item2(chatId, from, frid, cancelationToken))
                        return true;
                }
            }
            return false;
        }
        public async static Task DisplayDetailAsync(FundRaiser f, ChatId chatId, User tgUser, InlineKeyboardButton[][] keys, WithDrawalRequest request, bool showWithDrawwals)
        {
            WFDBService coreservice = await SendFunrRaiserPictureAsync(f, chatId);
            var stat = coreservice.GetStat(f.Id);
            var name = coreservice.GetAgent(f.AgentID).Name;
            var html = FormatDetail(f.Id, f.ShortName, f.ShortDescription, name, stat, f.TargetAmount, request, showWithDrawwals);
            if (keys != null && keys.Length > 0)
            {
                await theBotClient.SendTextMessageAsync(
                   chatId: chatId,
                   text: html,
                   parseMode: ParseMode.Html,
                   replyMarkup: new InlineKeyboardMarkup(keys)
               );
            }
            else
            {
                await theBotClient.SendTextMessageAsync(
                   chatId: chatId,
                   text: html,
                   parseMode: ParseMode.Html);
            }

        }

        private static async Task<WFDBService> SendFunrRaiserPictureAsync(FundRaiser f, ChatId chatId)
        {
            var coreservice = new WFDB.WFDBService();
            var pic = coreservice.GetFundRaiserPictures(f.Id);
            if (pic.Count > 0)
            {
                try
                {
                    var file = await theBotClient.GetFileAsync(pic[0].IdInExternalStorage);
                    await theBotClient.SendPhotoAsync(chatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(pic[0].IdInExternalStorage));
                }
                catch (Exception fex)
                {
                    Console.WriteLine("Error getting file " + fex.Message);
                }
            }

            return coreservice;
        }

        public static async Task<bool> ProcessShowDetailPrivate(ChatId chatId, User from,
            Guid frid,
            CancellationToken cancelationToken)
        {
            return await ProcessShowDetail(chatId, from, frid, true, cancelationToken);
        }
        public static async Task<bool> ProcessShowDetailGroup(ChatId chatId, User from,
            Guid frid,
            CancellationToken cancelationToken)
        {
            return await ProcessShowDetail(chatId, from, frid, false, cancelationToken);
        }
        public static String ContributeLink(Guid frid)
        {
            return $"https://telegram.me/{TGBot.meName}?start={INLINE_START_CONTRIBUTE}{frid}";
        }
        public static String ListContributionLink(Guid frid)
        {
            return $"https://telegram.me/{TGBot.meName}?start={INLINE_FR_LIST_CONT}{frid}";
        }

        public static async Task<bool> ProcessShowDetail(ChatId chatId,
            User from,
            Guid frid,
            bool privateChat,
            CancellationToken cancelationToken)
        {
            var coreservice = new WFDB.WFDBService();
            var f = coreservice.GetFundRaiser(frid);
            var agentID = coreservice.GetAgentChannelID(FundRaisingChannel.CHANNEL_TELEGRAM, f.AgentID);

            bool IsOwner = agentID != null && agentID.IdInChannel.Equals(from.Id.ToString());


            var keys = new List<InlineKeyboardButton[]>();
            if (privateChat && IsOwner && f.Status == FundRaiserStatus.Active)
            {
                keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Close", INLINE_FR_START_CLOSE + f.Id) ,
                    InlineKeyboardButton.WithCallbackData("Update.", INLINE_FR_START_UPDATE + f.Id) });
                keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Withdraw Money.", INLINE_FR_START_TRANSFER_REQUEST + f.Id) });
            }
            if (privateChat)
                keys.Add(new[] { InlineKeyboardButton.WithCallbackData("List contributions.", INLINE_FR_LIST_CONT + f.Id) });
            else
                keys.Add(new[] { InlineKeyboardButton.WithUrl("List contributions.", ListContributionLink(frid)) });
            //keys.Add(new[] { InlineKeyboardButton.WithSwitchInlineQuery("Add to Group.", "/start@"+meName) });
            if (f.Status == FundRaiserStatus.Active)
            {
                if (privateChat)
                    keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Contribute.", INLINE_START_CONTRIBUTE + f.Id) });
                else
                    keys.Add(new[] { InlineKeyboardButton.WithUrl("Contribute.", ContributeLink(frid)) });
            }
            WithDrawalRequest request = null;
            if (TGBot.IsBotAdmin(from))
            {
                request = coreservice.GetPendingWithdrawalRequest(f.Id);
                if (request != null)
                    keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Process transfer request.", INLINE_PROCESS_TRANSER + f.Id) });
            }
            await DisplayDetailAsync(f, chatId, from, keys.ToArray(), request, IsOwner);
            return true;
        }


        private static async Task<bool> ProcessProcessTransfer(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken)
        {
            await TGBot.PushDialog(from, new ProcessWithdrawalRequest(frid, from, chatId), cancelationToken);
            return true;
        }
        private static async Task<bool> ProcessContribute(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken)
        {
            await TGBot.PushDialog(from, new ContributDialog(chatId, from, frid), cancelationToken);
            return true;
        }
        private static async Task<bool> ProcessStartFrClose(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken)
        {
            var d = new CloseFRDialog(theBotClient, chatId, from, frid);
            await TGBot.PushDialog(from, d, cancelationToken);
            return true;
        }
        private async static Task<bool> ProcessStartFrUpdate(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken)
        {
            var d = new UpdateFRDialog(chatId, from, frid);
            await TGBot.PushDialog(from, d, cancelationToken);
            return true;

        }
        private static async Task<bool> ProcessStartFrTransfer(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken)
        {
            var d = new RequestWithdrawalDialg(frid, from, chatId);
            await TGBot.PushDialog(from, d, cancelationToken);
            return true;
        }
        private static async Task<bool> DisplayContributions(ChatId chatId, User from, Guid frid, CancellationToken cancelationToken)
        {
            var d = new ContributionListViewer(chatId, from, frid);
            await TGBot.PushDialog(from, d, cancelationToken);
            return true;

        }


        private static async Task<bool> ProcessCancel(ChatId chatId, String tgUserID)
        {
            await TGBot.ClearDialogAsync(chatId, tgUserID);            
            return true;
        }
        
        private static async Task<bool> ProcessStart(WFTGDB.WFTGDBService service, Message msg, TgUserState state)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(
                    TGBot.IsBotAdmin(msg.From) ? new KeyboardButton[][]
                    {
                        new KeyboardButton [] { MAIN_START_FR },
                        new KeyboardButton[] { MAIN_LIST_MY_FR },
                        new KeyboardButton[] { MAIN_LIST_WR_REQ },
                    } : new KeyboardButton[][]
                    {
                        new KeyboardButton [] { MAIN_START_FR },
                        new KeyboardButton[] { MAIN_LIST_FR },
                        new KeyboardButton[] { MAIN_LIST_MY_FR },

                    },
                    resizeKeyboard: true
                );

            await theBotClient.SendTextMessageAsync(
                chatId: msg.Chat.Id,
                text: "What do you want to do?",
                replyMarkup: replyKeyboardMarkup
            );
            return true;
        }


        static string FormatDetail(Guid frid, String title, string description, string name, WFDB.FundRaiserStat stat, long targetAmount, WithDrawalRequest request, bool showWithDrawals)
        {
            String progressText;
            if (targetAmount > -1)
            {
                if (stat.Count == 0)
                    progressText = $"Fund rasing target: {IntData.toString(targetAmount)} Birr";
                else
                    progressText = $"{IntData.toString(stat.Total)} of {IntData.toString(targetAmount)} Birr raised from {stat.Count} contribution{(stat.Count > 0 ? "s" : "")}";
            }
            else
                if (stat.Count == 0)
                progressText = $"No contribution yet";
            else
                progressText = $"{IntData.toString(stat.Total)} Birr raised from {stat.Count} contribution{(stat.Count > 1 ? "s" : "")}";
            if (request != null)
                progressText += "<pre>\n</pre>Transfer requested";
            if (showWithDrawals)
            {
                progressText += $"<pre>\n</pre>Gross Withdrawal: {IntData.toString(stat.TotalWithdrawal)} Birr";
                progressText += $"<pre>\n</pre>Commission: {IntData.toString(stat.TotalCommission)} Birr";
                progressText += $"<pre>\n</pre>Registration Fee: {IntData.toString(stat.TotalRegFee)} Birr";
                progressText += $"<pre>\n</pre>Net Paid: {IntData.toString(stat.TotalWithdrawal - stat.TotalRegFee - stat.TotalCommission)} Birr";
            }
            var html = $"<strong>{System.Web.HttpUtility.HtmlEncode(title)}</strong>";
            html += $"<pre>\n</pre>{System.Web.HttpUtility.HtmlEncode(description)}";
            html += $"<pre>\n</pre>{progressText}";
            html += $"<pre>\n</pre><a href=\"https://telegram.me/{TGBot.meName}?startgroup={JOIN_GROUP_PREFIX + frid}\">Add to a Group</a>";
            if (name != null)
                html += $"<pre>\n</pre><i>By {name}</i>";
            return html;
        }
        private static async Task<bool> DisplayFRList(ChatId chatID, TgUserState state, bool mine, bool requests, long groupId)
        {
            var wfservice = new WFDB.WFDBService();
            List<WFDB.FundRaiser> frs;
            if (mine)
            {
                var agent = state == null ? null : wfservice.GetAgentByChannel(FundRaisingChannel.CHANNEL_TELEGRAM, state.TgUserID);
                if (agent == null || (frs = wfservice.GetUserRaisers(agent.Id, 0, 5)).Count == 0)
                {
                    var text = "You haven't created a WeFund yet. You can create here.";
                    await theBotClient.SendTextMessageAsync(chatID, text);
                    return true;
                }
            }
            else if (requests)
            {
                frs = wfservice.GetTransferRequests(0, 5);
            }
            else if (groupId != -1)
            {
                frs = wfservice.GetGroupFundRaiser();
            }
            else
            {
                frs = wfservice.GetActiveFundRaisers(0, 5);
            }

            if (frs.Count == 0)
            {
                String text = null;
                if (requests)
                    text = "There is no request at this time. Check again latter.";
                else
                    text = "We appreciate your kindeness, however there are no active WeFund right now. Please try sometime laterr";
                await theBotClient.SendTextMessageAsync(chatID, text);
                return true;
            }

            foreach (var f in frs)
            {
                InlineKeyboardButton[][] buttons;
                if (groupId == -1)
                    buttons = new[]{
                        new []{InlineKeyboardButton.WithCallbackData("Contribute",INLINE_START_CONTRIBUTE+f.Id.ToString())}
                        ,new []{InlineKeyboardButton.WithCallbackData("Details", INLINE_START_DETAILS + f.Id.ToString())}
                };
                else
                    buttons = new[]{
                        new []{InlineKeyboardButton.WithUrl("Contribute",ContributeLink(f.Id)) }
                        ,new []{InlineKeyboardButton.WithUrl("List Contribution",ListContributionLink(f.Id))}
                };
                var stat = wfservice.GetStat(f.Id);
                var name = wfservice.GetAgent(f.AgentID).Name;
                var html = FormatDetail(f.Id, f.ShortName, f.ShortDescription, name, stat, f.TargetAmount, null, false);
                if (groupId != -1)
                {
                    await SendFunrRaiserPictureAsync(f, chatID);
                }
                await theBotClient.SendTextMessageAsync(chatID,
                    text: html,
                    replyMarkup: new InlineKeyboardMarkup(buttons),
                    parseMode: ParseMode.Html);
            }

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
                    if (await ProcessSelectedFR(update.CallbackQuery.From, update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Data, false, cancellationToken))
                        return true;
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
                                    if (await ProcessStart(service, msg, state()))
                                        return true;
                                }
                                else if (parts.Length == 2)
                                {
                                    if (await ProcessSelectedFR(msg.From, msg.Chat.Id, parts[1], true, cancellationToken))
                                        return true;
                                }
                            }
                            if (msg.Text.Equals(MAIN_LIST_FR, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await DisplayFRList(msg.Chat.Id, state(), false, false, -1))
                                    return true;
                            }
                            if (msg.Text.Equals(MAIN_LIST_MY_FR, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await DisplayFRList(msg.Chat.Id, state(), true, false, -1))
                                    return true;
                            }

                            if (msg.Text.Equals(MAIN_LIST_WR_REQ, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await DisplayFRList(msg.Chat.Id, state(), false, true, -1))
                                    return true;
                            }
                            if (msg.Text.Equals(MAIN_START_FR, StringComparison.CurrentCultureIgnoreCase))
                            {
                                await TGBot.PushDialog(msg.From, new CreateFRDialog(msg.Chat.Id, msg.From), cancellationToken);
                                return true;
                            }
                            if (msg.Text.Equals("/cancel", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (await ProcessCancel(msg.Chat.Id, msg.From.Id.ToString()))
                                    return true;
                            }
                        }
                    }
                    else if (msg.Chat.Type == ChatType.Supergroup || msg.Chat.Type == ChatType.Group)
                    {
                        if (msg.Type == MessageType.ChatMemberLeft)
                        {
                            if (msg.LeftChatMember.Id == botClient.BotId)
                                service.LeftGroup(msg.Chat.Id);
                        }
                        else if (msg.Type == MessageType.ChatMembersAdded)
                        {
                            if (msg.NewChatMembers != null)
                            {
                                foreach (var m in msg.NewChatMembers)
                                    if (m.Id == botClient.BotId)
                                    {
                                        service.JoinedGroup(msg.Chat.Id);
                                        break;
                                    }
                            }
                        }
                        else if (msg.Type == MessageType.Text)
                        {
                            var parts = msg.Text.Split(' ');
                            if (parts.Length == 1)
                            {
                                if (parts[0].StartsWith("/start", StringComparison.OrdinalIgnoreCase)
                                    || parts[0].StartsWith("/wefund", StringComparison.OrdinalIgnoreCase))
                                {
                                    await DisplayFRList(msg.Chat.Id, null, false, false, msg.Chat.Id);
                                }

                            }
                            if (parts.Length == 2 && parts[0].Equals("/start@" + TGBot.meName))
                            {
                                if (parts[1].StartsWith(JOIN_GROUP_PREFIX))
                                {
                                    if (Guid.TryParse(parts[1].Substring(JOIN_GROUP_PREFIX.Length), out var frid))
                                    {
                                        service.AddFRToGroup(msg.Chat.Id, frid);
                                        await ProcessShowDetail(msg.Chat.Id, new User
                                        {
                                            Id = botClient.BotId.Value,
                                            Username = TGBot.meName,
                                            FirstName = TGBot.meName,
                                            IsBot = true,
                                        }, frid, false, cancellationToken);
                                    }

                                }
                            }
                        }
                        return true;
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
