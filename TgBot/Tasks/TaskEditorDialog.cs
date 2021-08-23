using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.SmartLedger;
using TgBot.Tasks;
using static TgBot.FormDialog;

namespace TgBot.Tasks
{
    public class TaskEditorDialog : BotDialogBase
    {
        const string COMMAND_CHANGE_TITLE= "ChangeTitle";
        const string COMMAND_CHANGE_DESCRIPTION= "ChangeDescription";
        const string COMMAND_CHANGE_DUE_DATE= "ChangeDueDate";
        const string COMMAND_EDIT_CHECKLIST = "EditCheckList";
        const string COMMAND_EXIT = "DoneEditing";
        const string COMMAND_REMOVE = "Remove";
        const string COMMAND_DONT_REMOVE = "NoRemove";

        public Guid TaskId { get; set; }
        public String UserId { get; set; }
        public long ChatId() => long.Parse(UserId);
        public User User() => new User { Id = long.Parse(this.UserId), FirstName = "Default" };
        public int MessageId { get; set; }
        
        public String CurrentCommand{ get; set; } = null;
        public ContentData SelectedContentData { get; set; }

        public TaskEditorDialog(String userId, Guid taskId) 
        {
            this.TaskId = taskId;
            this.UserId = userId;
        }
        
        protected override async Task<DialogResult> HandleChildTerminate(ITelegramBotClient bot, Update update, IBotDialog child, CancellationToken cancellationToken)
        {
            var checkListEditor = child as CheckListEditorDialog;
            if (checkListEditor != null)
            {
                var service = new TaskDbService();
                var task = service.GetTask(this.TaskId);
                service.UpdateFullCheckList(this.UserId, this.TaskId, checkListEditor.CheckList);
                var notif = $"{service.GetUserProfile(this.UserId).FullName} changed the check list of task /{task.CodeName}";
                var notifGroup = $"{service.GetUserProfile(this.UserId).FullName} changed the check list of task {TaskBot.TaskLink(task.Id, task.CodeName)}";
                await TaskBot.NotifyGroups(bot, notifGroup, cancellationToken);
                await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, notif, this.UserId, cancellationToken);
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            var contentMenu = child as ContentItemMenu;
            if(contentMenu!=null)
            {
                var service = new TaskDbService();
                string notif = null;
                string notifGroup = null;
                var task = service.GetTask(this.TaskId);
                if (contentMenu.Remove())
                {
                    service.RemoveContent(this.UserId, this.TaskId, SelectedContentData.Id);
                    notif = $"{service.GetUserProfile(this.UserId).FullName} removed an attachment from task {task.CodeName}";
                    notifGroup = $"{service.GetUserProfile(this.UserId).FullName} removed an attachment from task {TaskBot.TaskLink(task.Id, task.CodeName)}";
                }
                else if (contentMenu.NewCaption() != null)
                {
                    service.SetContentCaption(this.UserId, SelectedContentData.Id, contentMenu.NewCaption());
                }

                if (notif != null)
                {
                    await TaskBot.NotifyGroups(bot, notifGroup, cancellationToken);
                    await TaskBot.NotifyWorkersAndFollowers(bot, this.TaskId, notif, this.UserId, cancellationToken);
                }
                await UpdateDetail(bot, true, cancellationToken);
                return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }

        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            var html = TaskBot.FormatTaskDetailHtml(this.TaskId,new TaskBot.TaskFormatOptions(){includeChkCommands=false,
                includeAttCommands = true
            });
            this.MessageId=(await bot.SendTextMessageAsync(ChatId(),
                html
                , parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
                ,replyMarkup: FormDialog.CreateInlineButtons(GetCommands(),2)
                )).MessageId;
            return DialogResult.Handled;
        }
        public List<KeyValuePair<String, String>> GetCommands()
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
                if (task.Status != Tasks.TaskStatus.Canceled && task.Status != Tasks.TaskStatus.Done && (isCreator || userRole == TaskUserRole.Worker))
                {
                    choices.Add(new(COMMAND_CHANGE_TITLE, "Change Title"));
                    choices.Add(new(COMMAND_CHANGE_DESCRIPTION, "Change Description"));
                    choices.Add(new(COMMAND_CHANGE_DUE_DATE, "Change Due Date"));
                    choices.Add(new(COMMAND_EDIT_CHECKLIST, "Edit Checklist"));

                }
                choices.Add(new(COMMAND_EXIT, "Done Editing"));
            }
            return choices;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancellationToken)
        {
            var service = new TaskDbService();
            Func<MisTask> task = () => service.GetTask(TaskId);
            Func<List<MisUserProfile>> taskusers = () => service.GetTaskUsers(TaskId, TaskUserRole.Worker);
            Func<MisUserProfile> user = () => service.GetUserProfile(this.UserId);

            switch (callBack.Data)
            {
                case COMMAND_CHANGE_TITLE:
                    await bot.SendTextMessageAsync(this.ChatId(), "Enter new title", cancellationToken: cancellationToken);
                    this.CurrentCommand = callBack.Data;
                    return DialogResult.Handled;
                case COMMAND_CHANGE_DESCRIPTION:
                    await bot.SendTextMessageAsync(this.ChatId(), "Enter new description", cancellationToken: cancellationToken);
                    this.CurrentCommand = callBack.Data;
                    return DialogResult.Handled;
                case COMMAND_CHANGE_DUE_DATE:
                    await bot.SendTextMessageAsync(this.ChatId(), "Enter new due date", cancellationToken: cancellationToken);
                    this.CurrentCommand = callBack.Data;
                    return DialogResult.Handled;
                case COMMAND_EDIT_CHECKLIST:
                    this.CurrentCommand = callBack.Data;
                    await this.SetChildDialog(bot, new CheckListEditorDialog(this.ChatId(), service.GetTaskCheckList(this.TaskId)), cancellationToken);
                    return DialogResult.Handled;
                case COMMAND_EXIT:
                    await this.HandleCancel(bot, cancellationToken);
                    return DialogResult.Terminated;
            }
            return DialogResult.Continue;
        }
        public override async Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
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
        public class ContentItemMenu : FormDialog
        {
            const string FIELD_COMMAND = "Command";
            const string FIELD_CONFIRM_REMOVE = "Confirm";
            const string FIELD_CAPTION = "Caption";
            const string CONFRIM_YES = "Yes";
            const string CMD_REMOVE = "Remove";
            const string CMD_SET_CAPTION = "SetCaption";
            public string NewCaption() => base.FieldData.ContainsKey(FIELD_CAPTION)?base.FieldData[FIELD_CAPTION].Val<String>():"";
            public bool Remove()=> base.FieldData.ContainsKey(FIELD_CONFIRM_REMOVE) && CONFRIM_YES.Equals(base.FieldData[FIELD_CONFIRM_REMOVE].Val());
            public ContentItemMenu():base(null,null)
            {

            }
            public ContentItemMenu(TaskEditorDialog parent, string name) : base(parent.ChatId(),parent.User())
            {

            }
            public override string FirstField => FIELD_COMMAND;
            public override FormDialogField GetFieldDef(string key)
            {
                switch (key)
                {
                    case FIELD_COMMAND:
                        return new FormDialogField
                        {
                            FieldType = FieldType.Choices,
                            PromptHtml = $"What do you want to do with <strong>name</strong> done?",
                            Choices = new[]
                            {
                                new FormFieldChoiceItem(CMD_REMOVE,"Remove it"),
                                new FormFieldChoiceItem(CMD_SET_CAPTION,"Set Caption"),
                            },
                            NextField=d=>Task.FromResult(d[FIELD_COMMAND].Val<String>().Equals(CMD_REMOVE)?FIELD_CONFIRM_REMOVE:FIELD_CAPTION),
                        };
                    case FIELD_CONFIRM_REMOVE:
                        return new FormDialogField
                        {
                            FieldType=FieldType.Choices,
                            PromptHtml = $"Are you sure you want to remove this attachment?",
                            Choices = new[]
                            {
                                new FormFieldChoiceItem(CONFRIM_YES),
                                new FormFieldChoiceItem("No"),
                            },
                            NextField = null

                        };
                    case FIELD_CAPTION:
                        return new FormDialogField
                        {
                            PromptHtml = $"Enter caption for your attachment:",                            
                            NextField = null

                        };
                }
                return null;
            }
        }

        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            var service = new TaskDbService();
            var task = service.GetTask(this.TaskId);
            var contents= service.GetTaskContentIndex(this.TaskId);
            if (message.Text.StartsWith("/att")) //checklist toggle
            {
                int index;
                if (int.TryParse(message.Text.Substring("/att".Length), out index) && index >= 1 && index <= contents.Count)
                {
                    this.SelectedContentData = contents[index - 1];
                    await base.SetChildDialog(bot, new ContentItemMenu(this,"Attachment "+index),cancelationToken);
                }
                return DialogResult.Handled;
            }
            if(CurrentCommand!=null)
            {
                var u = service.GetUserProfile(this.UserId);
                switch(CurrentCommand)
                {
                    case COMMAND_CHANGE_TITLE:
                        service.ChangeTitle(this.UserId, this.TaskId, message.Text);
                        CurrentCommand = null;
                        await UpdateDetail(bot, true, cancelationToken);
                        return DialogResult.Handled;
                    case COMMAND_CHANGE_DESCRIPTION:
                        service.ChangeDescription(this.UserId, this.TaskId, message.Text);
                        CurrentCommand = null;
                        await UpdateDetail(bot, true, cancelationToken);
                        return DialogResult.Handled;
                    case COMMAND_CHANGE_DUE_DATE:
                        if (DateTime.TryParse(message.Text, out var dd) && dd>TGBot.NowDt())
                        {
                            service.ChangeDueDate(this.UserId, this.TaskId, dd.Ticks);
                            CurrentCommand = null;
                            await UpdateDetail(bot, true, cancelationToken);
                        }
                        else
                        {
                            await bot.SendTextMessageAsync(this.ChatId(), "Enter valid date", cancellationToken: cancelationToken);
                        }
                        return DialogResult.Handled;
                }
            }
            return DialogResult.Continue;
        }

        private async Task UpdateDetail(ITelegramBotClient bot, bool delete, CancellationToken cancellationToken)
        {
            var html = TaskBot.FormatTaskDetailHtml(this.TaskId,
                new TaskBot.TaskFormatOptions() { includeChkCommands=false,
                includeAttCommands=true});
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
