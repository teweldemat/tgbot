using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.Tasks
{
    public abstract class PromptTaskListDialog : FormDialog
    {
        const string FIELD_SELECTION = "task";
        private const string NO_SELECTION = "No";

        public List<MisTask> TaskList { get; set; }
        TaskDbService service;
        public PromptTaskListDialog(TaskDbService service, List<MisTask> list, ChatId chatId, User user) : base(chatId, user)
        {
            this.TaskList = list;
            this.service = service;
        }
        public override string FirstField => FIELD_SELECTION;
        public abstract String PromptText { get; }
        public abstract String NoButtonLebel { get; }
        public override FormDialogField GetFieldDef(string key)
        {
            var list = TaskList.Select(x => new FormFieldChoiceItem
                  (
                      key: x.Id.ToString(),
                      name: $"{x.Code} - {x.Title}"
                  )).ToList();
            list.Add(new FormFieldChoiceItem(NO_SELECTION, this.NoButtonLebel));
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
        protected abstract Task<DialogResult> OnSelection(ITelegramBotClient bot, CancellationToken cancellationToken, Guid taskID);
        protected override Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var selection = this.FieldData[FIELD_SELECTION].Val() as string;
            if (NO_SELECTION.Equals(selection))
            {
                return OnNoSelection(bot, cancellationToken);
            }
            return OnSelection(bot, cancellationToken, Guid.Parse(selection));
        }

    }
    public class StartPlannedTask : PromptTaskListDialog
    {
        TaskDbService service;
        TgDbService tgService;
        public StartPlannedTask(TaskDbService service, TgDbService tgService, List<MisTask> list, ChatId chatId, User user) : base(service, list, chatId, user)
        {
            this.service = service;
            this.tgService = tgService;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<TaskDbService>();
            this.tgService = services.GetService<TgDbService>();
        }

        public override string PromptText => "You can start one of these tasks";

        public override string NoButtonLebel => "None";

        protected override async Task<DialogResult> OnSelection(ITelegramBotClient bot, CancellationToken cancellationToken, Guid taskID)
        {
            await TaskBot.StartPlannedTask(bot, tgService, cancellationToken, this.chatId, this.from, taskID);
            return DialogResult.Terminated;
        }
    }
}
