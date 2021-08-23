using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.Tasks
{
    public class JoinTaskMenuDialog:FormDialog
    {
        const String FIELD_SELECTION = "Selection";

        public override string FirstField => FIELD_SELECTION;
        public String Prompt { get; set; }
        enum JoinType
        {
            CreateNewTask,
            JoinExistingTask
        }
        public JoinTaskMenuDialog():base(null,null)
        {

        }
        public JoinTaskMenuDialog(String prompt, ChatId chatId,User user):base(chatId,user)
        {
            this.Prompt = prompt;
        }

        public override FormDialogField GetFieldDef(string key)
        {
            return new FormDialogField
            {
                FieldType = FieldType.Choices,
                Prompt = this.Prompt,
                Choices = new[] {
                    new FormFieldChoiceItem(JoinType.CreateNewTask.ToString(), "Start a New Task")
                    , new FormFieldChoiceItem(JoinType.JoinExistingTask.ToString(), "Join an Existing Task")
                },
                ParseFunction = (b, t, c) =>
                  {
                      return Task.FromResult(new ParseResult { Data = Enum.Parse<JoinType>(t) });
                  },
                NextField = null
            };
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            switch ((JoinType)this.FieldData[FIELD_SELECTION].Val())
            {
                case JoinType.CreateNewTask:
                    await TGBot.PushDialog(this.from.Id.ToString(), new CreateTaskDialog(this.chatId, this.from,
                        addCreator: true,
                        startImmidiately: true
                        ), cancellationToken);
                    break;
                default:
                    break;
            }
            return DialogResult.Terminated;
        }
    }
}
