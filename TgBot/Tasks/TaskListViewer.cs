using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.SmartLedger;
using TgBot.TgDb;

namespace TgBot.Tasks
{
    class TaskListViewer : BotDialogBase
    {
        private const int CONTLIST_PAGE_SIZE = 5;
        private const string PAGE_PREFIX = "PAGE_";
        public ChatId chatId;
        public User user;
        public int pageIndex;
        public int buttonMsgId;
        public string prevText;
        public bool ActiveOnly;
        public String filterByUser;
        public String filterText = null;
        public bool nextButtonShown = false;
        public TaskListViewer(ChatId chatId, User user, bool activeOnly, string filterByUser = null, string filterText = null)
        {
            this.chatId = chatId;
            this.user = user;
            this.ActiveOnly = activeOnly;
            this.filterByUser = filterByUser;
            this.filterText = filterText;
        }
        public override void SetServices(IServiceProvider services)
        {
        }
        public static string FormatPayment(TaskDbService coreService, MisTask task, String numLabel)
        {
            var html = $"{numLabel}";
            html += $"{task.Title} /{task.Code}<pre>\n   </pre>{TaskFullData.StatusString(task)}";
            return html;
        }
        async Task<bool> ShowPageAsync(ITelegramBotClient bot, int index, CancellationToken cancelationToken)
        {
            using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
            {
                var coreService = serviceProvider.GetService<TaskDbService>();
                int totalN;
                var tasks = filterByUser == null
                    ? coreService.GetActiveTasks(filterText == null, filterText == null, filterText, index, CONTLIST_PAGE_SIZE, out totalN)
                    : coreService.GetActiveTasksByUser(filterByUser, TaskUserRole.Worker, filterText, index, CONTLIST_PAGE_SIZE, out totalN);
                if (tasks.Count == 0)
                {
                    await bot.SendTextMessageAsync(chatId, filterText == null ? "No tasks found." : "No tasks found containing text:" + filterText);
                }
                else
                {
                    var n = 1;
                    String listHtml = null;
                    foreach (var f in tasks)
                    {
                        var html = FormatPayment(coreService, f, $"{index + n}. ");
                        listHtml = listHtml == null ? html : (listHtml + "\n" + html);
                        n++;
                    }
                    if (filterText != null)
                        listHtml += $"\nShowing tasks containing text: <i>{filterText}</i>.\nEnter /clear to remove the filter";
                    if (nextButtonShown)
                    {
                        await bot.EditMessageReplyMarkupAsync(chatId, this.buttonMsgId, new InlineKeyboardMarkup(new InlineKeyboardButton[0]));
                        this.nextButtonShown = false;
                    }
                    if (index + CONTLIST_PAGE_SIZE < totalN)
                    {
                        this.pageIndex = index + CONTLIST_PAGE_SIZE;
                        var buttons = new InlineKeyboardButton[][]
                            {
                        new[] { InlineKeyboardButton.WithCallbackData(Program.lm.Show_more_n_records(Math.Min(CONTLIST_PAGE_SIZE, totalN - (index + CONTLIST_PAGE_SIZE)))
                        +"\t (Or type a filter)"
                        , PAGE_PREFIX + pageIndex), }
                            };
                        var replyKeyboardMarkup = new InlineKeyboardMarkup(buttons.ToArray());
                        var msg = await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: listHtml,
                            parseMode: ParseMode.Html,
                            replyMarkup: replyKeyboardMarkup
                        );
                        this.nextButtonShown = true;
                        this.buttonMsgId = msg.MessageId;
                        this.prevText = listHtml;
                    }
                    else
                    {
                        await bot.SendTextMessageAsync(chatId,
                            text: listHtml,
                            parseMode: ParseMode.Html);
                    }

                }
                return (totalN > index + CONTLIST_PAGE_SIZE) || filterText != null;
            }
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            if (await ShowPageAsync(bot, 0, cancelationToken))
                return DialogResult.Handled;
            return DialogResult.Terminated;
        }
        public override async Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            if (nextButtonShown)
            {
                await bot.EditMessageReplyMarkupAsync(chatId, this.buttonMsgId, new InlineKeyboardMarkup(new InlineKeyboardButton[0]));
                this.nextButtonShown = false;
            }
            return DialogResult.Terminated;
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            if (message.Text.StartsWith("/"))
            {
                if (message.Text == "/clear")
                    this.filterText = null;
                else
                    return DialogResult.Continue;
            }
            else
                this.filterText = message.Text;

            if (nextButtonShown)
            {
                await bot.EditMessageReplyMarkupAsync(chatId, this.buttonMsgId, new InlineKeyboardMarkup(new InlineKeyboardButton[0]));
                this.nextButtonShown = false;
            }
            pageIndex = 0;
            if (await ShowPageAsync(bot, pageIndex, cancelationToken))
                return DialogResult.Handled;
            return DialogResult.Terminated;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            if (callBack.Data.IndexOf(PAGE_PREFIX) == 0)
            {
                if (await ShowPageAsync(bot, pageIndex, cancelationToken))
                    return DialogResult.Handled;
                return DialogResult.Terminated;
            }

            return DialogResult.Continue;
        }
    }
}
