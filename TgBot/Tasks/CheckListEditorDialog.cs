using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.Tasks
{
    public class CheckListEditorDialog : BotDialogBase
    {
        private const string MI_DELETE = "Delete";
        private const string MI_ORDER = "Order";
        private const string MI_REPLACE = "Replace";
        private const string MI_TOGGLE = "Toggle";
        public enum FormStateType
        {
            List,
            ReplaceItem,
            OrderItem
        }
        public FormStateType FormState { get; set; } = FormStateType.List;
        public List<TaskCheckListItem> CheckList { get; set; }
        public ChatId ChatId { get; set; }
        public int SelectedItem { get; set; } = 0;
        public int messageId { get; set; }
        public CheckListEditorDialog(ChatId chatId, List<TaskCheckListItem> items)
        {
            this.ChatId = chatId;
            this.CheckList = items;
        }
        public override void SetServices(IServiceProvider services)
        {
        }
        String ListString()
        {
            if (CheckList == null || !CheckList.Any())
                return "<Empty Checklist>";
            String ret = null;
            int n = 1;
            foreach (var item in CheckList)
            {
                var itemstr = $"/{n}. {(item.DoneTime == null ? "_" : "X")} {item.Name}";
                n++;
                ret = ret == null ? itemstr : ret + "\n" + itemstr;
            }
            return ret;
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            messageId = (await bot.SendTextMessageAsync(ChatId, ListString(), cancellationToken: cancelationToken)).MessageId;
            return DialogResult.Continue;
        }
        async Task UpdateList(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await bot.DeleteMessageAsync(ChatId, messageId);
            this.messageId = (await bot.SendTextMessageAsync(ChatId, ListString(), cancellationToken: cancelationToken)).MessageId;
        }
        class CheckListItemMenu : MenuDialogBase
        {
            public TaskCheckListItem Item { get; set; }
            public String SelectedMenuItem { get; set; }
            public CheckListItemMenu(ChatId chatId, User from, TaskCheckListItem item) : base(chatId, from)
            {
                this.Item = item;
            }
            public override void SetServices(IServiceProvider services)
            {
            }
            protected override IList<FormFieldChoiceItem> Choices => new FormDialog.FormFieldChoiceItem[] {
                                new(MI_DELETE, "Delete"),
                                new(MI_ORDER, "Change Order"),
                                new(MI_REPLACE, "Replace")
                            };

            protected override Task<DialogResult> OnItemSelected(ITelegramBotClient bot, string key, CancellationToken cancellationToken)
            {
                this.SelectedMenuItem = key;
                return Task.FromResult(DialogResult.Terminated);
            }
        }
        protected override async Task<DialogResult> HandleChildTerminate(ITelegramBotClient bot, Update update, IBotDialog child, CancellationToken cancellationToken)
        {
            var menu = child as CheckListItemMenu;
            switch (menu.SelectedMenuItem)
            {
                case MI_DELETE:
                    this.CheckList.RemoveAt(SelectedItem - 1);
                    await UpdateList(bot, cancellationToken);
                    break;
                case MI_REPLACE:
                    this.FormState = FormStateType.ReplaceItem;
                    await bot.SendTextMessageAsync(ChatId, "Enter check list item name", cancellationToken: cancellationToken);
                    break;
                case MI_ORDER:
                    this.FormState = FormStateType.OrderItem;
                    await bot.SendTextMessageAsync(ChatId, "Enter order number", cancellationToken: cancellationToken);
                    break;
                case MI_TOGGLE:
                    if (this.CheckList[SelectedItem - 1].DoneTime == null)
                        this.CheckList[SelectedItem - 1].DoneTime = TGBot.Now();
                    else
                        this.CheckList[SelectedItem - 1].DoneTime = null;
                    await UpdateList(bot, cancellationToken);
                    break;
            }
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
        {
            var txt = message.Text;

            if (txt.Equals("/end", StringComparison.OrdinalIgnoreCase))
                return DialogResult.Terminated;
            if (txt.StartsWith("/"))
            {
                txt = txt.Substring(1);
                int index;
                if (int.TryParse(txt, out index) && index >= 1 && index <= CheckList.Count())
                {
                    SelectedItem = index;
                    await SetChildDialog(bot,
                        new CheckListItemMenu(message.Chat.Id, message.From, this.CheckList[SelectedItem - 1]),
                        cancellationToken);
                    return DialogResult.Handled;
                }
            }
            switch (FormState)
            {
                case FormStateType.List:
                    this.CheckList.Add(new TaskCheckListItem { Name = message.Text, OrderN = this.CheckList.Count + 1 });
                    break;
                case FormStateType.ReplaceItem:
                    this.CheckList[SelectedItem - 1].Name = message.Text;
                    FormState = FormStateType.List;
                    break;
                case FormStateType.OrderItem:
                    int on;
                    if (int.TryParse(message.Text, out on) && on >= 1 && on < this.CheckList.Count && on != SelectedItem)
                    {
                        var insertPos = (on > SelectedItem ? on - 1 : on) - 1;
                        var item = this.CheckList[this.SelectedItem - 1];
                        this.CheckList.RemoveAt(this.SelectedItem - 1);
                        this.CheckList.Insert(insertPos, item);
                    }
                    else
                        await bot.SendTextMessageAsync(ChatId, "Not a valid order number", cancellationToken: cancellationToken);
                    FormState = FormStateType.List;
                    break;
                default:
                    break;
            }
            await UpdateList(bot, cancellationToken);
            return DialogResult.Handled;
        }
    }
}
