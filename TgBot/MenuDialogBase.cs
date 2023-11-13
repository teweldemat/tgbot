using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot
{
    public abstract class MenuDialogBase : FormDialog
    {
        public string SelectedKey { get; set; }
        protected virtual int NButtonCols => 1;
        protected virtual Task<DialogResult> OnItemSelected(Telegram.Bot.ITelegramBotClient bot, String key, CancellationToken cancellationToken)
        {
            this.SelectedKey = key;
            return Task.FromResult(DialogResult.Terminated);
        }
        protected abstract IList<FormFieldChoiceItem> Choices { get; }

        public String Prompt { get; set; }
        public MenuDialogBase() : base(null, null)
        {

        }
        public MenuDialogBase(ChatId chatId, User from,
            String prompt = "Choose"
            ) : base(chatId, from)
        {
            this.Prompt = prompt;
        }
        public override string FirstField => "Choice";

        public override FormDialogField GetFieldDef(string key)
        {
            return new FormDialogField
            {
                PromptHtml = Prompt,
                Choices = this.Choices,
                FieldType = FieldType.Choices,
                ChoicesCol = NButtonCols,
                NextField = null
            };
        }
        protected override Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            return OnItemSelected(bot, FieldData[this.FirstField].Val() as string, cancellationToken);
        }
    }
    public class MenuDialog : MenuDialogBase
    {
        public IList<FormFieldChoiceItem> ChoicesList { get; set; }

        protected override IList<FormFieldChoiceItem> Choices => ChoicesList;

        public MenuDialog(ChatId chatId, User from,
            IList<FormFieldChoiceItem> choices,
                    String prompt = "Choose"
                    ) : base(chatId, from, prompt)
        {
            this.Prompt = prompt;
            this.ChoicesList = choices;
        }
        public override void SetServices(IServiceProvider services)
        {
        }
    }
}
