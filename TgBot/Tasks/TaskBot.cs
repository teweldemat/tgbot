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

namespace TgBot.Tasks
{
    public class TaskBot : ITGBotApp
    {
        public static String WebLinkBaseUrl;
        const string MAIN_PING_WORKER = "Ping";
        const string MAIN_CREATE_TASK = "New Task";
        const string MAIN_DUTY_STATION = "Duty Station";
        //const string MAIN_REQUEST_LEAVE = "Request Leave";
        //const string MAIN_REQUEST_REMOTE_WORK = "Request Remotework";

        const string MAIN_LIST_TASKS = "Browse Tasks";
        const string MI_ALL_TASKS = "List Active Tasks";
        const string MI_MY_TASKS = "My Tasks";
        //const string MAIN_LIST_OEPN_REQUESTS = "List Open Requests";        
        const string MAIN_SETTING = "Advanced";
        const string MI_SETUP_FLOW = "Configure Task System";
        const string MI_ADD_DUTY_STATION = "Add Duty Station";
        const string MI_SET_USER_PROFILE = "Set User Profile";
        const string MI_LIST_DUTY_STATIONS = "List Duty Stations";
        const string MI_ASSIGN_DS = "Assign Duty Station";
        const string MI_STATUS_REPORTS = "StatusReports";
        const string MI_FLOW_REPORTS = "FlowReports";


        private const int TOTAL_PING_INTERVAL = 5 * 1000; //five seconds

        public string BotAppName => "Tasks";
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
            var service = new TaskDbService();
            var prof = service.GetUserProfile(from.Id.ToString());
            if (prof == null)
            {
                await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Welcome");
                await TGBot.PushDialog(from.Id.ToString(), new SetUserProfileDialog(chatId, from), cancellationToken);
                return true;
            }
            var entity = service.GetEntity();
            if (entity == null)
            {
                await bot.SendTextMessageAsync(
                chatId: chatId,
                text: "Welcome.");
                await TGBot.PushDialog(from.Id.ToString(), new SetupCompanyDialog(chatId, from), cancellationToken);
                return true;
            }

            var buttons = new List<KeyboardButton[]>();
            if (prof.Permitted)
            {
                var ds = service.GetUserDutyStation(from.Id.ToString(), TGBot.Now());
                buttons.Add(new KeyboardButton[] { MAIN_CREATE_TASK });
                if (ds != null)
                    buttons.Add(new KeyboardButton[] { MAIN_DUTY_STATION });

                buttons.Add(new KeyboardButton[] { MAIN_LIST_TASKS });
                buttons.Add(new KeyboardButton[] { MAIN_SETTING });
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

        internal static async Task StartPlannedTask(ITelegramBotClient bot, CancellationToken cancellationToken,
            ChatId chatId, User user, Guid taskID)
        {
            var service = new TaskDbService();
            var task = service.GetTask(taskID);
            var prof = service.GetUserProfile(user.Id.ToString());
            service.StartTask(user.Id.ToString(), taskID);
            var message = $"{prof.FullName} started planned task {task.CodeName}";
            await NotifyTaskChange(bot, cancellationToken, task, message, false);
        }

        private static async Task NotifyTaskChange(ITelegramBotClient bot, CancellationToken cancellationToken, MisTask task, string message, bool html)
        {
            await NotifyGroups(bot, message, html, cancellationToken);
        }
        internal static async Task NotifyFollower(ITelegramBotClient bot, Guid taskId, String message, bool html, CancellationToken cancellationToken)
        {
            var service = new TaskDbService();
            var followers = service.GetTaskUsers(taskId, TaskUserRole.Follower);
            foreach (var follower in followers)
            {
                await bot.SendTextMessageAsync(
                        chatId: follower.UserId,
                        text: message,
                        parseMode: html ? ParseMode.Html : ParseMode.Default
                    );
            }
        }
        internal static async Task NotifyWorkers(ITelegramBotClient bot, Guid taskId, String message, CancellationToken cancellationToken)
        {
            var service = new TaskDbService();
            var followers = service.GetTaskUsers(taskId, TaskUserRole.Worker);
            foreach (var follower in followers)
            {
                await NotifyUser(bot, follower.UserId, message, cancellationToken);
            }
        }
        internal static async Task NotifyWorkersAndFollowers(ITelegramBotClient bot, Guid taskId, String message, String self, CancellationToken cancellationToken)
        {
            var service = new TaskDbService();
            foreach (var follower in service.GetTaskUsers(taskId, TaskUserRole.Worker))
            {
                if (follower.UserId.Equals(self))
                    continue;
                await NotifyUser(bot, follower.UserId, message, cancellationToken);
            }
            foreach (var follower in service.GetTaskUsers(taskId, TaskUserRole.Follower))
            {
                if (follower.UserId.Equals(self))
                    continue;
                await NotifyUser(bot, follower.UserId, message, cancellationToken);
            }
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
        public static String TaskLink(Guid guid, String linkText)
        {
            return $"<a href=\"{WebLinkBaseUrl}/task/?id={guid}\">{linkText}</a>";
        }
        public static String GetLedgerLink(Guid accountId)
        {
            return $"{SmartLedgerBot.WebLinkBaseUrl}/task/ledger?accountid={accountId}";
        }


        private static async Task<bool> ProcessCancel(ITelegramBotClient bot, ChatId chatId, String tgUserID, CancellationToken cancellationToken)
        {
            await TGBot.ClearDialogAsync(bot, chatId, tgUserID, cancellationToken);
            return true;
        }
        public class PerformanceReportMenu:BotDialogBase
        {
            
            const String FIELD_TYPE = "Type";
            const String DATE_FROM = "From";
            const String DATE_TO = "To";
            
            const String TYPE_TODAY = "Today";
            const String TYPE_YESTERDAY = "Yesterday";
            const String TYPE_LAST_ONE_WEEK = "LastWeek";
            const String TYPE_THIS_WEEK = "ThisWeek";
            const String TYPE_THIS_MONTH = "ThisMonth";
            const String TYPE_LAST_MONTH = "LastMonth";
            const String TYPE_DATE_REANGE = "DateRange";
            public string CurrentField = FIELD_TYPE;
            public ChatId ChatId;
            public long From;
            public long To;
            public int messageId;
            public PerformanceReportMenu(ChatId chatId) 
            {
                this.ChatId = chatId;
            }
            
            public override async Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken)
            {
                await base.HandleCancel(bot, cancelationToken);
                try
                {
                    await bot.EditMessageReplyMarkupAsync(ChatId,messageId);
                }
                catch(Exception ex)
                {
                    TGBot.LogException("Error trying to remove buttons", ex);
                }
                return DialogResult.Terminated;
            }
            public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
            {
                var buttons = new List<InlineKeyboardButton>();
                var dt = TGBot.NowDt().Date;
                buttons.Add(InlineKeyboardButton.WithUrl("Today", $"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={dt.Ticks}&to={dt.AddDays(1).Ticks}"));
                buttons.Add(InlineKeyboardButton.WithUrl("Yesterday", $"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={dt.AddDays(-1).Ticks}&to={dt.Ticks}"));
                var weekStart = dt.AddDays(-(int)dt.DayOfWeek);
                buttons.Add(InlineKeyboardButton.WithUrl("This Week", $"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={weekStart.Ticks}&to={dt.AddDays(1).Ticks}"));
                buttons.Add(InlineKeyboardButton.WithUrl("Last Week", $"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={weekStart.AddDays(-7).Ticks}&to={weekStart.Ticks}"));
                var monthStart = new DateTime(dt.Year, dt.Month, 1);
                buttons.Add(InlineKeyboardButton.WithUrl("This Month", $"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={monthStart.Ticks}&to={dt.AddDays(1).Ticks}"));
                buttons.Add(InlineKeyboardButton.WithUrl("Last Month", $"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={monthStart.AddMonths(-1).Ticks}&to={monthStart.Ticks}"));
                buttons.Add(InlineKeyboardButton.WithCallbackData("Date Range", DATE_FROM));
                var twoColumnButtons = new List<InlineKeyboardButton[]>();
                for(int i=0;i<buttons.Count;i+=2)
                {
                    var item1 = i;
                    var item2 = i + 1;
                    if (item2 < buttons.Count)
                        twoColumnButtons.Add(new InlineKeyboardButton[] { buttons[item1], buttons[item2] });
                    else
                        twoColumnButtons.Add(new InlineKeyboardButton[] { buttons[item1]});
                }
                messageId=(await bot.SendTextMessageAsync(this.ChatId, "Choose performance report type",
                    replyMarkup: new InlineKeyboardMarkup(twoColumnButtons.ToArray()),
                    cancellationToken: cancelationToken)).MessageId;
                return DialogResult.Handled;
            }
            public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
            {
                switch (callBack.Data)
                {
                    case DATE_FROM:
                        await bot.SendTextMessageAsync(this.ChatId, "Date from?", cancellationToken: cancelationToken);
                        CurrentField = DATE_FROM;
                        return DialogResult.Handled;
                }
                return DialogResult.Continue;
            }
            public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
            {
                if (message.Type != MessageType.Text)
                    return DialogResult.Continue;
                if(!DateTime.TryParse(message.Text,out var dt))
                    await bot.SendTextMessageAsync(this.ChatId, "Please enter valid date", cancellationToken: cancelationToken);
                switch(CurrentField)
                {
                    case DATE_FROM:
                        await bot.SendTextMessageAsync(this.ChatId, "Date to?", cancellationToken: cancelationToken);
                        this.From = dt.Ticks;
                        CurrentField = DATE_TO;
                        return DialogResult.Handled;
                    case DATE_TO:
                        await bot.SendTextMessageAsync(this.ChatId, "Date to?", cancellationToken: cancelationToken);
                        if (dt.Ticks < From)
                        {
                            await bot.SendTextMessageAsync(this.ChatId, "'Date to' cant be before 'date from'", cancellationToken: cancelationToken);
                            return DialogResult.Handled;
                        }
                        else
                        {
                            this.To = dt.AddDays(1).Ticks;
                            await bot.SendTextMessageAsync(this.ChatId, $"<a href=\"{TaskBot.WebLinkBaseUrl}/task/changesummary?from={this.From}&to={this.To}\">Click here to see flow report for dates from {new DateTime(From).Date.ToString("MMM dd,yyy")} to {new DateTime(To).Date.AddDays(-1).ToString("MMM dd,yyy")}</a>",
                                parseMode:ParseMode.Html,
                                cancellationToken: cancelationToken);
                            return DialogResult.Terminated;
                        }                        

                }
                return DialogResult.Continue;
            }

        }
        public class AdvancedMenu : MenuDialogBase
        {
            public AdvancedMenu(ChatId chatId, User from) :
                base(chatId, from)
            {

            }
            protected override int NButtonCols => 2;
            protected override IList<FormFieldChoiceItem> Choices
            {
                get
                {
                    var service = new TaskDbService();
                    var entity = service.GetEntity();
                    bool isOwner = entity.Owner.Equals(this.from.Id.ToString());
                    var config = service.GetTaskConfiguration<TaskConfigurationData>();
                    var flowConfigured = config != null;
                    var isHrManager = config != null && config.HRManager.Equals(this.from.Id.ToString());

                    var choices = new List<FormDialog.FormFieldChoiceItem>();
                    if (isHrManager || isOwner)
                    {
                        choices.Add(new FormDialog.FormFieldChoiceItem(MI_ADD_DUTY_STATION, "Add Duty Station"));
                    }
                    choices.Add(new FormDialog.FormFieldChoiceItem(MI_LIST_DUTY_STATIONS, "List Duty Stations"));
                    if (isOwner)
                        choices.Add(new FormDialog.FormFieldChoiceItem(MI_SETUP_FLOW, "Setup HR Flow"));
                    if ((isHrManager || isOwner) && service.DutyStationsCount() > 0)
                        choices.Add(new FormDialog.FormFieldChoiceItem(MI_ASSIGN_DS, "Assign Duty Station"));
                    choices.Add(new FormDialog.FormFieldChoiceItem(MI_STATUS_REPORTS, "Status Reports"));
                    choices.Add(new FormDialog.FormFieldChoiceItem(MI_FLOW_REPORTS, "Task Flow Reports"));
                    choices.Add(new FormDialog.FormFieldChoiceItem(MI_SET_USER_PROFILE, "Set Name"));
                    return choices;
                }
            }

            protected override async Task<DialogResult> OnItemSelected(ITelegramBotClient bot, string key, CancellationToken cancellationToken)
            {
                switch (key)
                {
                    case MI_ADD_DUTY_STATION:
                        await TGBot.PushDialog(from.Id.ToString(), new AddDutyStationDialog(chatId, from), cancellationToken);
                        return DialogResult.Terminated;
                    case MI_SET_USER_PROFILE:
                        await TGBot.PushDialog(from.Id.ToString(), new SetUserProfileDialog(chatId, from), cancellationToken);
                        return DialogResult.Terminated;
                    case MI_SETUP_FLOW:
                        await TGBot.PushDialog(from.Id.ToString(), new SetTaskFlowDialog(chatId, from), cancellationToken);
                        return DialogResult.Terminated;
                    case MI_LIST_DUTY_STATIONS:
                        await TGBot.PushDialog(from.Id.ToString(), new DutyStationListViewer(chatId, from), cancellationToken);
                        return DialogResult.Terminated;
                    case MI_ASSIGN_DS:
                        await TGBot.PushDialog(from.Id.ToString(), new AssignDutyStationDialog(chatId, from), cancellationToken);
                        return DialogResult.Terminated;
                    case MI_STATUS_REPORTS:
                        await bot.SendTextMessageAsync(
                            chatId: this.chatId,
                            text: "Select Report Type",
                            replyMarkup: new InlineKeyboardMarkup(new[] {
                                new[] { InlineKeyboardButton.WithUrl("Team Status", $"{WebLinkBaseUrl}/task/team")
                                , InlineKeyboardButton.WithUrl("My Status", $"{WebLinkBaseUrl}/task/user?userid={this.from.Id.ToString()}")}
                                ,new[] { 
                                    InlineKeyboardButton.WithUrl("Tasks List", $"{WebLinkBaseUrl}/task/activetasks")
                                ,InlineKeyboardButton.WithUrl("Visualize", $"{WebLinkBaseUrl}/task/planchart")
                                }
                            }),
                            cancellationToken: cancellationToken);
                        return DialogResult.Terminated;
                    case MI_FLOW_REPORTS:
                        await TGBot.PushDialog(from.Id.ToString(), new PerformanceReportMenu(chatId), cancellationToken);
                        return DialogResult.Terminated;

                }
                return DialogResult.Terminated;
            }
        }

        public class TaskListMenu : MenuDialogBase
        {
            public TaskListMenu(ChatId chatId, User from) :
                base(chatId, from)
            {

            }
            protected override int NButtonCols => 2;
            protected override IList<FormFieldChoiceItem> Choices
            {
                get
                {
                    var choices = new List<FormDialog.FormFieldChoiceItem>();
                    choices.Add(new FormDialog.FormFieldChoiceItem(MI_MY_TASKS, "My Tasks"));
                    choices.Add(new FormDialog.FormFieldChoiceItem(MI_ALL_TASKS, "All Tasks"));
                    return choices;
                }
            }

            protected override async Task<DialogResult> OnItemSelected(ITelegramBotClient bot, string key, CancellationToken cancellationToken)
            {
                switch (key)
                {
                    case MI_MY_TASKS:
                        await TGBot.PushDialog(this.from.Id.ToString(), new TaskListViewer(this.chatId, this.from, true,this.from.Id.ToString() ), cancellationToken);
                        return DialogResult.Terminated;
                    case MI_ALL_TASKS:
                        await TGBot.PushDialog(this.from.Id.ToString(), new TaskListViewer(this.chatId, this.from, true), cancellationToken);
                        return DialogResult.Terminated;

                }
                return DialogResult.Terminated;
            }
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
                    var service = new TaskDbService();
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