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

namespace TgBot.WeBirrAdmin
{
    public class WeBirrAdminBot : ITGBotApp
    {
        public static String WebLinkBaseUrl;
        const string MAIN_REGISTER_MERCHANT= "Register Merchant";
        const string MAIN_BECOME_MERCHANT_ADMIN = "Register as Merchant Admin";
        const string MAIN_ASSIGN_ADMIN = "Assign WeBirr Admin";
        const string MAIN_BLOCK_USER = "Block Telegram User";
        const string MAIN_REPORTS = "Reports";
        const string MAIN_CHECK_PAYMENT = "Check Payment";
        public string BotAppName => "WeBirr Admin";
        public Task<bool> HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
       
        private static async Task<bool> ProcessStart(ITelegramBotClient bot,
            ChatId chatId, User from, String message,
            CancellationToken cancellationToken)
        {
            var service = new WeBirrAdminDBService();
            var prof = service.GetUserProfile(from.Id.ToString());
            if (prof == null)
            {
                await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Welcome");
                await TGBot.PushDialog(from.Id.ToString(), new SetUserProfileDialog<WeBirrAdminDb>(chatId, from), cancellationToken);
                return true;
            }
            var entity = service.GetEntity();
            if (entity == null)
            {
                await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Welcome.");
                await TGBot.PushDialog(from.Id.ToString(), new SetupCompanyDialog<WeBirrAdminDb>(chatId, from), cancellationToken);
                return true;
            }
            bool isOwner = from.Id.ToString().Equals(entity.Owner);
            var wbaUser = service.GetWbaUserProfile(from.Id.ToString());
            var buttons = new List<KeyboardButton[]>();
            if (prof.Permitted)
            {
                if (isOwner)
                    buttons.Add(new KeyboardButton[] { MAIN_ASSIGN_ADMIN });
                if (wbaUser != null && wbaUser.IsSuperUser)
                {
                    buttons.Add(new KeyboardButton[] { MAIN_REGISTER_MERCHANT });
                    buttons.Add(new KeyboardButton[] { MAIN_BLOCK_USER });
                }

                if (wbaUser != null && wbaUser.MerchantPermissions.Count > 0)
                {
                    buttons.Add(new KeyboardButton[] { MAIN_REPORTS });
                    buttons.Add(new KeyboardButton[] { MAIN_CHECK_PAYMENT });
                }
                else
                {
                    buttons.Add(new KeyboardButton[] { MAIN_BECOME_MERCHANT_ADMIN });
                }
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

       

        internal static Task NotifyGroups(ITelegramBotClient bot, String message, CancellationToken cancellationToken)
            => NotifyGroups(bot, message, true, cancellationToken);
        internal static async Task NotifyGroups(ITelegramBotClient bot, String message, bool html, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var g in new TgDb.TgDbService().GetAllJoinedTGGroups(TGBot.BotToken))
                {
                    await bot.SendTextMessageAsync(
                            chatId: g.TgGroupId,
                            text: message,
                            parseMode: html ? ParseMode.Html : ParseMode.Default
                        );
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
                    var service = new WeBirrAdminDBService();
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
                            if (MI_SET_USER_PROFILE.Equals(msg.Text))
                            {
                                await TGBot.PushDialog(msg.From.Id.ToString(), new SetUserProfileDialog(msg.Chat.Id, msg.From), cancellationToken);
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

                            if (msg.Text.StartsWith("/fr_cuttoff"))
                            {
                                if (ProcessFRCuttoff(msg, service))
                                    return true;
                            }

                            switch (msg.Text)
                            {
                                case MAIN_SETTING:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new AdvancedMenu(
                                        msg.Chat.Id,
                                        msg.From), cancellationToken);
                                    return true;

                                case MAIN_PING_WORKER:
                                    await PingTask(service, service.GetUserProfile(msg.From.Id.ToString()));
                                    return true;
                                case MAIN_CREATE_TASK:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new CreateTaskDialog(
                                        msg.Chat.Id,
                                        msg.From,
                                        startImmidiately: false
                                        ), cancellationToken);
                                    return true;
                                case MAIN_DUTY_STATION:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new OnDutyCheckDialog(msg.Chat.Id, msg.From, true), cancellationToken);
                                    return true;
                                case MAIN_LIST_TASKS:
                                    await TGBot.PushDialog(msg.From.Id.ToString(), new TaskListMenu(msg.Chat.Id, msg.From), cancellationToken);
                                    return true;
                            }
                        }
                    }
                    break;
            }
            return false;
        }

        private static bool ProcessFRCuttoff(Message msg, TaskDbService service)
        {
            if (!service.GetEntity().Owner.Equals(msg.From.Id.ToString()))
                return false;
            var parts = msg.Text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                return false;
            if (!DateTime.TryParse(parts[1], out var dt))
                return false;
            if (!int.TryParse(parts[2], out var ver))
                return false;
            service.SetCurrentFRVersion(msg.From.Id.ToString(), dt.Ticks, ver);
            return true;
        }

        public class TaskFormatOptions
        {
            public bool includeDetailLink = true;
            public bool includeChkCommands = true;
            public bool includeAttCommands = false;
        }
        public static String FormatTaskDetailHtml(Guid taskId, TaskFormatOptions options)
        {

            var service = new TaskDbService();
            var p = service.GetTask(taskId);
            var checkList = service.GetTaskCheckList(taskId);
            var content = service.GetTaskContents(taskId);
            var statusString = TaskFullData.StatusString(p);
            var text = $"<strong>Code</strong> {p.Code}";
            text += $"<pre>\n</pre><strong>Title</strong> {p.Title}";
            text += $"<pre>\n</pre><strong>Status:</strong> {statusString}";
            if (p.PlannedEndTime != null)
                text += $"<pre>\n</pre><strong>Due time:</strong> {TGBot.ToRelativeTime(p.PlannedEndTime.Value)}";
            if (!String.IsNullOrEmpty(p.Description))
                text += $"<pre>\n</pre><strong>Description:</strong>{p.Description}";
            text += $"<pre>\n</pre><strong>Created by:</strong> {service.GetUserProfile(p.CreatedBy).FullName}";
            if (p.ParentTaskId != null)
            {
                var parent = service.GetTask(p.ParentTaskId.Value);
                text += $"<pre>\n</pre><strong>Subtask of:</strong> {parent.Title} /{parent.Code}";
            }
            if (checkList.Count > 0)
            {
                text += "<pre>\n</pre><strong>Check List</strong>";
                int n = 1;
                foreach (var c in checkList)
                {

                    text += $"<pre>\n</pre>";
                    if (options.includeChkCommands)
                        text += $"<pre> </pre>/chk{n++}";
                    text += $"<pre> </pre>{(c.DoneTime == null ? "_" : "X")} : {c.Name}";
                }

            }
            if (content.Count > 0)
            {
                text += "<pre>\n</pre><strong>Attachments</strong>";
                int n = 1;
                foreach (var c in content)
                {
                    text += $"<pre>\n</pre>";
                    if (options.includeAttCommands)
                        text += $"<pre> </pre>/att{n}";
                    if (c.LinkType != FormDialog.ContentLinkType.Url)
                        text += $"<pre> </pre><a href=\"{WebLinkBaseUrl}/task/file?id={c.Id}\">{(String.IsNullOrEmpty(c.Caption) ? "Attachment " + n : c.Caption)}</a>";
                    else
                        text += $"<pre> </pre><a href=\"{c.ContentLink}\">{(String.IsNullOrEmpty(c.Caption) ? "Attachment " + n : c.Caption)}</a>";
                    n++;
                }
            }
            
            var subtasks = service.GetSubTasks(taskId);
            if (subtasks.Count > 0)
            {
                text += "<pre>\n</pre><strong>Subtasks</strong>";
                foreach (var c in subtasks)
                {
                    text += $"<pre>\n</pre>/{c.Code} {c.Title} ({TaskFullData.StatusString(c)})";
                }
            }
            int commentCount = service.GetTaskCommentCount(taskId);
            if (commentCount > 0)
            {
                var comment = service.GetLastComment(taskId);
                var commenter = service.GetUserProfile(comment.UserId);
                text += $"<pre>\n</pre>Comment by {commenter.Name()} ({TGBot.ToRelativeTime(comment.Time)})";
                text += $"<pre>\n</pre><i>{comment.Comment}</i>";
                if(commentCount>2)
                    text += $"<pre>\n</pre>{commentCount-1} other comments not shown";
                else if(commentCount==2)
                    text += $"<pre>\n</pre>One other comment not shown";
            }

            var workers = service.GetTaskUsers(taskId, TaskUserRole.Worker);
            if (workers.Count == 0)
                text += "<pre>\n</pre>No one is assigned to this task";
            else
            {
                text += "<pre>\n</pre><strong>Working on it:</strong>";
                text += workers[0].FullName;
                for (int i = 1; i < workers.Count; i++)
                    text += ", " + workers[i].FullName;
            }
            if (options.includeDetailLink)
                text += $"<pre>\n</pre> <a href=\"{WebLinkBaseUrl}/task/?id={taskId}\">Details</a>";
            return text;
        }

        static bool runPingThread = true;
        static async Task PingTask(TaskDbService service, MisUserProfile user)
        {
            try
            {
                var workState = service.GetUserWorkState(user.UserId);
                var u = new User() { Id = long.Parse(workState.UserId), FirstName = "Unknown" };
                var c = new CancellationTokenSource().Token;
                workState.SetBotContext(TGBot.Bot, u.Id, u, c);
                await workState.Ping(TGBot.Now());
                service.SaveWorkerState(workState);
            }
            catch (Exception ex)
            {
                TGBot.LogException($"Error pinging user {user.UserId}", ex);
            }

        }
        static async Task RunTaskPing()
        {
            var service = new TaskDbService();
            while (runPingThread)
            {
                var users = service.GetActiveUserProfiles();
                var interval = TOTAL_PING_INTERVAL / users.Count;
                foreach (var user in users)
                {
                    await PingTask(service, user);
                    await Task.Delay(interval);
                }
            }
        }
        public void OnBotConnect(ITelegramBotClient botClient)
        {
            WebLinkBaseUrl = Program.GeneralConfiguration("Smartledger", "Web");
            var t = RunTaskPing();
        }

        public async Task<bool> HandleUpdateResidualAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type==UpdateType.Message && update.Message.Chat.Type == ChatType.Private && update.Message.Type==MessageType.Text)
            {
                var msg = update.Message;
                var msgTxt=msg.Text.Trim();
                bool taxCodeQuery = false;
                foreach (var pr in new[] { MisTask.TASK_CODE_PREFIX })
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
                    var service = new TaskDbService();
                    var task = service.GetTaskByCode(taskCode);
                    if (task != null)
                    {
                        await TGBot.PushDialog(msg.From.Id.ToString(), new TaskDetailDialog(msg.From.Id.ToString(), task.Id), cancellationToken);
                        return true;
                    }
                }
                if(msgTxt.Length>3)
                {
                    await TGBot.PushDialog(msg.From.Id.ToString(), new TaskListViewer(msg.Chat.Id, msg.From,false, filterText: msgTxt), cancellationToken);
                    return true;
                }
                await ProcessStart(botClient, msg.Chat.Id, msg.From, "Sorry, I don't understand what you are trying to say.\nAs a bot, I can sometimes be dumb.", cancellationToken);
            }
            return false;
        }
    }
}