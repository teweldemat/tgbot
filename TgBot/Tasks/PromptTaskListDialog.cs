using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.Tasks
{
    public abstract class PromptTaskListDialog : FormDialog
    {
        const string FIELD_SELECTION = "task";
        private const string NO_SELECTION = "No";

        public List<MisTask> TaskList { get; set; }
        public PromptTaskListDialog(List<MisTask> list,ChatId chatId,User user):base(chatId,user)
        {
            this.TaskList = list;
        }
        public override string FirstField => FIELD_SELECTION;
        public abstract String PromptText { get; }
        public abstract String NoButtonLebel { get; }
        public override FormDialogField GetFieldDef(string key)
        {
            var service = new TaskDbService();
            var list = TaskList.Select(x => new FormFieldChoiceItem
                  (
                      key: x.Id.ToString(),
                      name: $"{x.Code} - {x.Title}"
                  )).ToList();
            list.Add(new FormFieldChoiceItem(NO_SELECTION,this.NoButtonLebel));
            return new FormDialogField
            {
                Choices = list,
                FieldType = FieldType.Choices,
                Prompt = this.PromptText,
            };
        }
        protected virtual Task<DialogResult> OnNoSelection(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            return Task.FromResult(DialogResult.Terminated);
        }
        protected abstract Task<DialogResult> OnSelection(ITelegramBotClient bot, CancellationToken cancellationToken,Guid taskID);
        protected override Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var selection = this.FieldData[FIELD_SELECTION].Val() as string;
            if(NO_SELECTION.Equals(selection))
            {
                return OnNoSelection(bot,cancellationToken);
            }
            return OnSelection(bot,cancellationToken,Guid.Parse(selection));
        }

    }
    public class StartPlannedTask:PromptTaskListDialog
    {
        public StartPlannedTask(List<MisTask> list, ChatId chatId, User user) : base(list,chatId, user)
        {

        }
        public override string PromptText => "You can start one of these tasks";

        public override string NoButtonLebel => "None";

        protected override async Task<DialogResult> OnSelection(ITelegramBotClient bot, CancellationToken cancellationToken, Guid taskID)
        {
            await TaskBot.StartPlannedTask(bot, cancellationToken,this.chatId, this.from, taskID);
            return DialogResult.Terminated;
        }
    }
}
