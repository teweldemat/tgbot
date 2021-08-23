using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.SmartLedger;
using TgBot.Tasks;
using static TgBot.FormDialog;

namespace TgBot.Tasks
{
    public class TaskDetailDialog : BotDialogBase
    {
        //manage task
        
        const String COMMAND_ADD_DOCUMENT = "AddDoc";
        const String COMMAND_UPDATE = "Modify";
        const String COMMAND_COMMENT = "Comment";
        
        const String COMMAND_COMMENT_REMOVE = "RemoveComment";
        const String COMMAND_COMMENT_UPDATE = "UpdateComment";

        const string COMMAND_FOLLOW = "Follow";
        const string COMMAND_JOIN = "Join";
        const string COMMAND_LEAVE_TASK= "Leave";
        const string COMMAND_UNFOLLOW_TASK = "Unfollow";

        const string COMMAND_ADD_USER= "AddUser";
        const string COMMAND_REMOVE_USER = "RemoveUser";

        const string COMMAND_CREATE_SUB_TASK = "CrateSubTask";
        const string COMMAND_CREATE_SUB_TASK_NEW = "CrateSubTaskNew";
        const string COMMAND_CREATE_SUB_TASK_EXISTING= "CrateSubTaskExisting";
        //state commands
        const string COMMAND_START = "Start";
        const string COMMAND_SUSPEND = "Susped";
        const string COMMAND_FINISHED = "Finished";
        const string COMMAND_RESTART = "Restart";
        const string COMMAND_CANCEL= "Cancel";

        //checklist
        const string COMMAND_DONE = "Done";
        const string COMMAND_NOT_DONE = "Not Done";
        
        public Guid TaskId { get; set; }
        public String UserId { get; set; }
        public long ChatId() => long.Parse(UserId);
        public User User() => new User { Id = long.Parse(this.UserId), FirstName = "Default" };
        public int MessageId { get; set; }
        public enum CommandStatus
        {
            Main,
            CheckList,
            SuspendReason,
            Comment,
            CommentReplace,
        }
        public CommandStatus Status { get; set; } = CommandStatus.Main;
        public TaskCheckListItem SelectedCheckListItem { get; set; }

        public TaskDetailDialog(String userId, Guid taskId) 
        {
            this.TaskId = taskId;
            this.UserId = userId;
        }
        public class AddSubTaskDialog:BotDialogBase
        {
            public Guid TaskId { get; set; }
            public ChatId ChatId { get; set; }
            public String UserId { get; set; }
            public AddSubTaskDialog()
            {

            }
            public AddSubTaskDialog(TaskDetailDialog parent)
            {
                this.ChatId = parent.ChatId();
                this.TaskId = parent.TaskId;
                this.UserId = parent.UserId;
            }
            public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
            {
                if (message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
                {
                    var service = new TaskDbService();
                    var task = service.GetTaskByCode(message.Text
                        .ToUpper()
                        .Replace(" ", "")
                        .Replace("/", ""));
                    if (task == null)
                    {
                        await bot.SendTextMessageAsync(this.ChatId, "No task found with code: " + message.Text, cancellationToken: cancelationToken);
                        return DialogResult.Terminated;
                    }
                    var parent = service.GetTask(TaskId);
                    try
                    {
                        service.AddSubTask(this.UserId, this.TaskId, task.Id);
                        var u = service.GetUserProfile(this.UserId);
                        var notif = $"{u.FullName} add subs task {task.CodeName} to {parent.CodeName}";
                        var notifgroup = $"{u.FullName} add subs task {task.CodeName} to {TaskBot.TaskLink(task.Id, task.CodeName)}";
                        await TaskBot.NotifyGroups(bot, notif, cancelationToken);
                        await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, notifgroup, this.UserId, cancelationToken);
                    }
                    catch (Exception ex)
                    {
                        await bot.SendTextMessageAsync(this.ChatId, $"Sorry we couldn't make {task.CodeName} a sub task of {parent.CodeName}\n{ex.Message}", cancellationToken: cancelationToken);
                        return DialogResult.Terminated;

                    }

                    return DialogResult.Terminated;
                }
                return DialogResult.Continue;
            }
        }
        protected override async Task<DialogResult> HandleChildTerminate(ITelegramBotClient bot, Update update, IBotDialog child, CancellationToken cancellationToken)
        {
            if (child is CommentMenu)
            {
                var service = new TaskDbService();
                this.Status = CommandStatus.Main;
                var menu = child as CommentMenu;
                switch (menu.SelectedKey)
                {
                    case COMMAND_COMMENT:
                        {
                            await bot.SendTextMessageAsync(this.ChatId(), "Enter comment", cancellationToken: cancellationToken);
                            this.Status = CommandStatus.Comment;
                            return DialogResult.Handled;
                        }
                    case COMMAND_COMMENT_REMOVE:
                        {
                            var u = service.GetUserProfile(this.UserId);
                            var task = service.GetTask(this.TaskId);
                            service.RemoveComment(this.UserId, service.GetLastComment(this.TaskId).Id);
                            await TaskBot.NotifyGroups(bot, $"{u.FullName} removed {Program.lm._pronoun_possessive(u.Gender)} comment on task {TaskBot.TaskLink(this.TaskId, task.CodeName)}",cancellationToken);
                            await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, $"{u.FullName} removed {Program.lm._pronoun(u.Gender)} comment on task /{task.CodeName}", this.UserId, cancellationToken);
                            await this.UpdateDetail(bot, true, cancellationToken);

                            return DialogResult.Handled;
                        }
                    case COMMAND_COMMENT_UPDATE:
                        {
                            await bot.SendTextMessageAsync(this.ChatId(), "Enter new comment", cancellationToken: cancellationToken);
                            this.Status = CommandStatus.CommentReplace;
                            return DialogResult.Handled;
                        }

                }
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            if (child is AddSubTaskDialog)
            {
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            if(child is SubTaskMenu)
            {
                var service = new TaskDbService();
                this.Status = CommandStatus.Main;
                var menu = child as SubTaskMenu;
                switch (menu.SelectedKey)
                {
                    case COMMAND_CREATE_SUB_TASK_NEW:
                        {
                            await SetChildDialog(bot, new CreateTaskDialog(this.ChatId(), this.User(), parentTaskId: this.TaskId), cancellationToken);
                            return DialogResult.Handled;
                        }
                    case COMMAND_CREATE_SUB_TASK_EXISTING:
                        {
                            await SetChildDialog(bot, new AddSubTaskDialog(this), cancellationToken);
                            return DialogResult.Handled;
                        }
                }
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            if(child is CreateTaskDialog)
            {
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            var checkListMenu = child as CheckListMenu;
            if(checkListMenu!=null)
            {
                var service = new TaskDbService();
                this.Status = CommandStatus.Main;

                switch (checkListMenu.SelectedKey)
                {
                    case COMMAND_DONE:
                        if (SelectedCheckListItem.DoneTime == null)
                        {
                            
                            SelectedCheckListItem.DoneTime = TGBot.Now();
                            service.UpdateCheckList(this.UserId, this.TaskId, new[] { SelectedCheckListItem });
                            await UpdateDetail(bot, true, cancellationToken);
                            var task = service.GetTask(this.TaskId);
                            var notif = $"{service.GetUserProfile(this.UserId).FullName} reported check list item: <strong>{this.SelectedCheckListItem.Name}</strong> of task {TaskBot.TaskLink(task.Id, task.CodeName)} as DONE";
                            await TaskBot.NotifyGroups(bot, notif, cancellationToken);
                            return DialogResult.Handled;
                        }
                        break;
                    case COMMAND_NOT_DONE:
                        if (SelectedCheckListItem.DoneTime != null)
                        {
                            SelectedCheckListItem.DoneTime = null;
                            service.UpdateCheckList(this.UserId, this.TaskId, new[] { SelectedCheckListItem });
                            await UpdateDetail(bot, true, cancellationToken);
                            var task = service.GetTask(this.TaskId);
                            var notif = $"{service.GetUserProfile(this.UserId).FullName} reported check list item: <strong>{this.SelectedCheckListItem.Name}</strong> of task {TaskBot.TaskLink(task.Id, task.CodeName)} as NOT DONE";
                            await TaskBot.NotifyGroups(bot, notif, cancellationToken);
                            return DialogResult.Handled;
                        }
                        break;
                }
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }

            var userListMenu = child as UserListMenu;
            if (userListMenu != null)
            {
                var service = new TaskDbService();
                var thisUser = service.GetUserProfile(this.UserId);
                var user = service.GetUserProfile(userListMenu.SelectedKey);
                var task = service.GetTask(this.TaskId);

                switch (userListMenu.Command)
                {
                    case COMMAND_ADD_USER:
                        service.AddTaskUser(this.UserId, this.TaskId, userListMenu.SelectedKey,TaskUserRole.Worker);
                        await TaskBot.NotifyGroups(bot, $"{user.FullName} is added to task: {TaskBot.TaskLink(this.TaskId, task.CodeName)} by {thisUser.FullName}",true,cancellationToken);
                        await TaskBot.NotifyUser(bot,userListMenu.SelectedKey, $"You are added to task: {TaskBot.TaskLink(this.TaskId, task.CodeName)} by {thisUser.FullName}.",cancellationToken);
                        break;
                    case COMMAND_REMOVE_USER:
                        service.RemoveTaskUser(this.UserId, this.TaskId, userListMenu.SelectedKey);
                        await TaskBot.NotifyGroups(bot, $"{user.FullName} is removed from task: {TaskBot.TaskLink(this.TaskId, task.CodeName)} by {thisUser.FullName}", true, cancellationToken);
                        await TaskBot.NotifyUser(bot, userListMenu.SelectedKey, $"You are removed from task: {TaskBot.TaskLink(this.TaskId, task.CodeName)} by {thisUser.FullName}.", cancellationToken);
                        break;
                }
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            var content = child as ContentListDialog;
            if(content!=null)
            {
                var service = new TaskDbService();
                var thisUser = service.GetUserProfile(this.UserId);
                service.AddTaskContents(this.UserId, this.TaskId,content.Data());
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;

            }
            return DialogResult.Continue;
        }

        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            var html = TaskBot.FormatTaskDetailHtml(this.TaskId,new TaskBot.TaskFormatOptions());
            this.MessageId=(await bot.SendTextMessageAsync(ChatId(),
                html
                , parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
                ,replyMarkup: FormDialog.CreateInlineButtons(GetCommands(),2)
                )).MessageId;
            return DialogResult.Handled;
        }
        public List<KeyValuePair<String,String>> GetCommands()
        {

            var service = new TaskDbService();
            var user = service.GetUserProfile(this.UserId);
            var choices = new List<KeyValuePair<String, String>>();
            if (user != null && user.Permitted)
            {
                var task = service.GetTask(TaskId);
                var taskWorkers = service.GetTaskUsers(task.Id, TaskUserRole.Worker);
                var taskFollowers = service.GetTaskUsers(task.Id, TaskUserRole.Follower);
                var config = service.GetTaskConfiguration<TaskConfigurationData>();
                var userRole = taskWorkers.Where(x => x.UserId.Equals(user.UserId)).Any() ? TaskUserRole.Worker :
                    (taskFollowers.Where(x => x.UserId.Equals(user.UserId)).Any() ? TaskUserRole.Follower : TaskUserRole.None);
                var isCreator = user.UserId.Equals(task.CreatedBy);
                switch (task.Status)
                {
                    case Tasks.TaskStatus.Planned:
                    case Tasks.TaskStatus.Waiting:
                        if (userRole == TaskUserRole.Worker)
                        {
                            choices.Add(new(COMMAND_START, "Started"));
                            choices.Add(new(COMMAND_CANCEL, "Canceled"));
                        }                            
                        break;
                    case Tasks.TaskStatus.Started:
                        if (userRole == TaskUserRole.Worker)
                        {
                            choices.Add(new(COMMAND_FINISHED, "Finished"));
                            choices.Add(new(COMMAND_SUSPEND, "Suspeded"));
                            choices.Add(new(COMMAND_CANCEL, "Canceled"));
                        }
                        break;
                    case Tasks.TaskStatus.Canceled:
                    case Tasks.TaskStatus.Done:
                        if (userRole == TaskUserRole.Worker || isCreator)
                            choices.Add(new(COMMAND_RESTART, "Restarted"));
                        break;
                    default:
                        break;
                }
                if (userRole == TaskUserRole.Worker || isCreator)
                {
                    choices.Add(new (COMMAND_ADD_DOCUMENT, "Add Document"));
                    choices.Add(new (COMMAND_UPDATE, "Modify Task Info"));
                }
                
                //choices.Add(new (COMMAND_COMMENT, "Comment"));
                if (userRole == TaskUserRole.None)
                    choices.Add(new (COMMAND_FOLLOW, "Follow Task"));
                if (userRole != TaskUserRole.Worker)
                    choices.Add(new (COMMAND_JOIN, "Join Task"));
                if (isCreator || userRole == TaskUserRole.Worker)
                {
                    if (userRole == TaskUserRole.Follower)
                        choices.Add(new(COMMAND_UNFOLLOW_TASK, "Stop Following Task"));
                    if (userRole == TaskUserRole.Worker)
                        choices.Add(new(COMMAND_LEAVE_TASK, "Leave Task"));
                    choices.Add(new (COMMAND_ADD_USER, "Add Member"));
                    choices.Add(new (COMMAND_REMOVE_USER, "Remove Member"));
                    choices.Add(new(COMMAND_CREATE_SUB_TASK, "Add Subtask"));
                }
                choices.Add(new(COMMAND_COMMENT, "Comment"));
            }
            return choices;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancellationToken)
        {
            if (this.Status == CommandStatus.Main)
            {
                var service = new TaskDbService();
                Func<MisTask> task = () => service.GetTask(TaskId);
                Func<List<MisUserProfile>> taskusers = ()=>service.GetTaskUsers(TaskId, TaskUserRole.Worker);
                Func<MisUserProfile> user = () => service.GetUserProfile(this.UserId);

                switch (callBack.Data)
                {
                    case COMMAND_COMMENT:
                        var comment = service.GetLastComment(this.TaskId);
                        if (comment != null && comment.UserId.Equals(this.UserId))
                        {
                            await base.SetChildDialog(bot, new CommentMenu(this), cancellationToken);
                        }
                        else
                        {
                            await bot.SendTextMessageAsync(this.ChatId(), "Enter comment", cancellationToken: cancellationToken);
                            this.Status = CommandStatus.Comment;
                        }
                        return DialogResult.Handled;
                    case COMMAND_START:
                    case COMMAND_RESTART:
                        {
                            service.StartTask(this.UserId, this.TaskId);
                            var u = user();
                            var t = task();
                            await TaskBot.NotifyGroups(bot, $"{u.FullName} started task {TaskBot.TaskLink(t.Id, t.CodeName)}",cancellationToken);
                            await TaskBot.NotifyWorkersAndFollowers(bot,this.TaskId, $"{u.FullName} started task /{t.CodeName}", this.UserId, cancellationToken);
                            await this.UpdateDetail(bot,true,cancellationToken);
                            return DialogResult.Handled;
                        }
                    case COMMAND_FINISHED:
                        {
                            service.FinishTask(this.UserId, this.TaskId);
                            var u = user();
                            var t = task();
                            await TaskBot.NotifyGroups(bot, $"{u.FullName} finished task {TaskBot.TaskLink(t.Id, t.CodeName)}", cancellationToken);
                            await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, $"{u.FullName} finished task /{t.CodeName}", this.UserId, cancellationToken);
                            await this.UpdateDetail(bot, true, cancellationToken);
                            return DialogResult.Handled;
                        }
                    case COMMAND_CANCEL:
                        {
                            service.CancelTask(this.UserId, this.TaskId);
                            var u = user();
                            var t = task();
                            await TaskBot.NotifyGroups(bot, $"{u.FullName} canceled task {TaskBot.TaskLink(t.Id, t.CodeName)}", cancellationToken);
                            await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, $"{u.FullName} canceled task /{t.CodeName}", this.UserId, cancellationToken);
                            await this.UpdateDetail(bot, true,cancellationToken);
                            return DialogResult.Handled;
                        }
                    case COMMAND_SUSPEND:
                        {
                            Status = CommandStatus.SuspendReason;
                            await bot.SendTextMessageAsync(this.ChatId(), "What is this task waiting for?", cancellationToken: cancellationToken);
                            return DialogResult.Handled;
                        }
                    case COMMAND_ADD_USER:
                        {
                            var list = service.GetAllUserProfiles().Where(x => !taskusers().Where(y => y.UserId == x.UserId).Any());
                            if (list.Any())
                            {
                                await base.SetChildDialog(bot, new UserListMenu(this, COMMAND_ADD_USER
                                    , list
                                    , "Whom are you adding?")
                                    , cancellationToken
                                    );
                            }
                            else
                                await bot.SendTextMessageAsync(ChatId(), "There is no one left to add", cancellationToken: cancellationToken);
                            
                            return DialogResult.Handled;
                        }
                    case COMMAND_REMOVE_USER:
                        {
                            var tu = taskusers();
                            if (tu.Any())
                            {
                                await base.SetChildDialog(bot, new UserListMenu(this, COMMAND_REMOVE_USER
                                , tu
                                , "Whom are you removing?")
                                , cancellationToken
                                );
                            }
                            else
                                await bot.SendTextMessageAsync(ChatId(), "There is no one left to remove", cancellationToken: cancellationToken);
                            return DialogResult.Handled;
                        }
                    case COMMAND_CREATE_SUB_TASK:
                        {
                            await SetChildDialog(bot, new SubTaskMenu(this),cancellationToken);
                            return DialogResult.Handled;
                        };
                    case COMMAND_JOIN:
                        service.AddTaskUser(this.UserId, this.TaskId, this.UserId, TaskUserRole.Worker);
                        await TaskBot.NotifyGroups(bot, $"{user().FullName} joined task: {TaskBot.TaskLink(this.TaskId,task().CodeName)}", true, cancellationToken);
                        await this.UpdateDetail(bot, true, cancellationToken);
                        return DialogResult.Handled;
                    case COMMAND_LEAVE_TASK:
                        service.RemoveTaskUser(this.UserId, this.TaskId, this.UserId);
                        await TaskBot.NotifyGroups(bot, $"{user().FullName} left task: {TaskBot.TaskLink(this.TaskId, task().CodeName)}", true, cancellationToken);
                        await this.UpdateDetail(bot, true, cancellationToken);
                        return DialogResult.Handled;
                    case COMMAND_FOLLOW:
                        service.AddTaskUser(this.UserId, this.TaskId, this.UserId, TaskUserRole.Follower);
                        await this.UpdateDetail(bot, true, cancellationToken);
                        return DialogResult.Handled;
                    case COMMAND_UNFOLLOW_TASK:
                        service.RemoveTaskUser(this.UserId, this.TaskId, this.UserId);
                        await this.UpdateDetail(bot, true, cancellationToken);
                        return DialogResult.Handled;
                    case COMMAND_ADD_DOCUMENT:
                        await this.SetChildDialog(bot,new ContentListDialog(this.UserId, "Add urls and attachments"), cancellationToken);
                        return DialogResult.Handled;
                    case COMMAND_UPDATE:
                        await TGBot.PushDialog(this.UserId, new TaskEditorDialog(this.UserId, this.TaskId), cancellationToken);
                        return DialogResult.Handled;
                }
            }
            else if (this.Status == CommandStatus.CheckList)
            {
                switch (callBack.Data)
                {
                    
                }
                await UpdateDetail(bot, true, cancellationToken); //nothing changed
                return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }
        public override async Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await base.HandleCancel(bot, cancelationToken);
            try
            {
                await bot.EditMessageReplyMarkupAsync(this.ChatId(), this.MessageId, 
                    new InlineKeyboardMarkup(new InlineKeyboardButton[] { }));
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to remove buttons", ex);
            }
            return DialogResult.Terminated;
        }
        public class CheckListMenu:MenuDialog
        {
            public CheckListMenu():base(null,null,null)
            {

            }
            public CheckListMenu(TaskDetailDialog parent):
                base(parent.ChatId(),parent.User(), new[] {
                        new FormFieldChoiceItem(TaskDetailDialog.COMMAND_DONE,"Yes, it is done"),
                        new FormFieldChoiceItem(TaskDetailDialog.COMMAND_NOT_DONE,"No, it isn't done"),
                        },
                    $"Is check list item <strong>{parent.SelectedCheckListItem.Name}</strong> done?")
            {

            }
        }
        public class UserListMenu : MenuDialog
        {
            public String Command { get; set; }
            public UserListMenu():base(null,null,null)
            {

            }
            public UserListMenu(TaskDetailDialog parent,
                String command,
                IEnumerable<MisUserProfile> users,String prompt) :
                base(parent.ChatId(), 
                    parent.User(), 
                    users.Select(x=>new FormFieldChoiceItem(x.UserId,x.FullName)).ToList(),
                    prompt)
            {
                this.Command=command;
            }
        }
        public class SubTaskMenu : MenuDialog
        {
            public SubTaskMenu() : base(null, null, null)
            {

            }
            public SubTaskMenu(TaskDetailDialog parent) :
                base(parent.ChatId(), parent.User(), new[] {
                        new FormFieldChoiceItem(TaskDetailDialog.COMMAND_CREATE_SUB_TASK_NEW,"Net Task"),
                        new FormFieldChoiceItem(TaskDetailDialog.COMMAND_CREATE_SUB_TASK_EXISTING,"Existing Task"),
                        },
                    $"Choose")
            {

            }
        }

        public class CommentMenu: MenuDialog
        {
            public CommentMenu() : base(null, null, null)
            {

            }
            public CommentMenu(TaskDetailDialog parent) :
                base(parent.ChatId(), parent.User(), new[] {
                    new FormFieldChoiceItem(TaskDetailDialog.COMMAND_COMMENT,"New Comment"),
                        new FormFieldChoiceItem(TaskDetailDialog.COMMAND_COMMENT_UPDATE,"Update Comment"),
                        new FormFieldChoiceItem(TaskDetailDialog.COMMAND_COMMENT_REMOVE,"Remove Comment"),
                        },
                    $"Choose")
            {

            }
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            var service = new TaskDbService();
            var task = service.GetTask(this.TaskId);
            var chk= service.GetTaskCheckList(this.TaskId);
            if (message.Text.StartsWith("/chk")) //checklist toggle
            {
                int index;
                if (int.TryParse(message.Text.Substring("/chk".Length), out index) && index >= 1 && index <= chk.Count)
                {
                    this.Status = CommandStatus.CheckList;
                    this.SelectedCheckListItem = chk[index - 1];
                    await base.SetChildDialog(bot, new CheckListMenu(this),cancelationToken);
                }
                return DialogResult.Handled;
            }
            if(Status==CommandStatus.Comment)
            {
                service.AddComment(this.UserId, this.TaskId, message.Text);
                Status = CommandStatus.Main;
                var u = service.GetUserProfile(this.UserId);
                await TaskBot.NotifyGroups(bot, $"{u.FullName} commented on task {TaskBot.TaskLink(this.TaskId, task.CodeName)}\n<i>{HttpUtility.HtmlEncode(message.Text)}</i>", cancelationToken);
                await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, $"{u.FullName} commented on task /{task.CodeName}\n<i>{HttpUtility.HtmlEncode(message.Text)}</i>", this.UserId, cancelationToken);
                await this.UpdateDetail(bot, true, cancelationToken);
                return DialogResult.Handled;
            }
            if (Status == CommandStatus.CommentReplace)
            {
                service.AddComment(this.UserId, this.TaskId, message.Text);
                Status = CommandStatus.Main;
                var u = service.GetUserProfile(this.UserId);
                await TaskBot.NotifyGroups(bot, $"{u.FullName} updated {Program.lm._pronoun_possessive(u.Gender)} comment on task {TaskBot.TaskLink(this.TaskId, task.CodeName)}\n<i>{HttpUtility.HtmlEncode(message.Text)}</i>", cancelationToken);
                await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, $"{u.FullName} updated {Program.lm._pronoun(u.Gender)} comment on task /{task.CodeName}\n<i>{HttpUtility.HtmlEncode(message.Text)}</i>", this.UserId, cancelationToken);
                await this.UpdateDetail(bot, true, cancelationToken);
                return DialogResult.Handled;
            }
            if (Status==CommandStatus.SuspendReason)
            {
                service.PauseTask(this.UserId, this.TaskId,message.Text);
                var u = service.GetUserProfile(this.UserId);
                await TaskBot.NotifyGroups(bot, $"{u.FullName} suspeded task {TaskBot.TaskLink(task.Id, task.CodeName)}", cancelationToken);
                await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, $"{u.FullName} suspeded task {task.CodeName}", this.UserId, cancelationToken);
                await this.UpdateDetail(bot, true, cancelationToken);
                return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }

        private async Task UpdateDetail(ITelegramBotClient bot, bool delete, CancellationToken cancellationToken)
        {
            var html = TaskBot.FormatTaskDetailHtml(this.TaskId,new TaskBot.TaskFormatOptions());
            if (delete)
            {
                await bot.DeleteMessageAsync(ChatId(), MessageId);
                this.MessageId = (await bot.SendTextMessageAsync(ChatId(),
                        html
                        , parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
                        , replyMarkup: FormDialog.CreateInlineButtons(GetCommands(),2)
                        )).MessageId;
            }
            else
            {
                await bot.EditMessageTextAsync(ChatId(), this.MessageId,
                            html,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: FormDialog.CreateInlineButtons(GetCommands(),2)
                    );
            }
        }
    }
}
