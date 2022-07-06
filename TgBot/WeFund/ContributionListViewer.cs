using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.Dialogs
{
    class ContributionListViewer :BotDialogBase
    {
        private const int CONTLIST_PAGE_SIZE = 5;
        private const string PAGE_PREFIX = "PAGE_";
        public ChatId chatId;
        public User user;
        public Guid frid;
        public int pageIndex;
        public int buttonMsgId;
        public string prevText;

        public ContributionListViewer(ChatId chatId, User user, Guid frid)
        {
            this.chatId = chatId;
            this.user = user;
            this.frid = frid;
        }
        public static string FormatContribution(WFDB.WFDBService coreService, WFDB.Contribution contribution,WFDB.FundRaiser fr,String numLabel,bool includeContributeLink)
        {

            String name;
            if (contribution.Anonymous)
                name = Program.lm.Someone;
            else if(contribution.Alias==null)
            {
                name = contribution.Alias;
            }
            else
                name= coreService.GetAgent(contribution.AgentID.Value).Name;
            var html = $"{numLabel}"
                + Program.lm.Contributed_to_fundraiser($"<strong>{name}</strong>",fr.ShortName, IntData.toString(contribution.Amount))
                + $"{(String.IsNullOrEmpty(contribution.Note) ? "" : "<pre>\n</pre>" + contribution.Note)}<pre>\n</pre><i>{(contribution.Time <= 0 ? "" : IntData.toDateString(contribution.Time, "MMM dd,yy hh:mm"))}</i>";
            if (includeContributeLink)
                html += $"<pre>\n</pre><a href=\"{WeFundBotApp.ContributeLink(fr.Id)}\">{Program.lm.Contribute}</a>";
            return html;
        }
        async Task<bool> ShowPageAsync(ITelegramBotClient bot,int index, CancellationToken cancelationToken)
        {
            
            var coreService = new WFDB.WFDBService();
            var fr = coreService.GetFundRaiser(frid);
            var conts = coreService.GetContributions(frid, index, CONTLIST_PAGE_SIZE, out var totalN);
            if (conts.Count == 0)
            {
                await bot.SendTextMessageAsync(chatId, Program.lm.No_contributions_yet__Work_on_the_promotion);
            }
            else
            {
                var n = 1;
                String listHtml = null;
                foreach (var f in conts)
                {
                    var html = FormatContribution(coreService, f, fr,$"{index+n} .",false);
                    listHtml = listHtml == null ? html : (listHtml + "<pre>\n</pre>" + html);
                    n++;
                }

                if(index>0)
                {
                    await bot.EditMessageTextAsync(chatId, this.buttonMsgId, this.prevText, ParseMode.Html);
                }
                if (index + CONTLIST_PAGE_SIZE < totalN)
                {
                    this.pageIndex = index + CONTLIST_PAGE_SIZE;
                    var buttons = new InlineKeyboardButton[][]
                        {
                        new[] { InlineKeyboardButton.WithCallbackData(Program.lm.Show_more_n_records(Math.Min(CONTLIST_PAGE_SIZE, totalN - (index + CONTLIST_PAGE_SIZE))), PAGE_PREFIX + pageIndex), }
                        };
                    var replyKeyboardMarkup = new InlineKeyboardMarkup(buttons.ToArray());
                    var msg=await bot.SendTextMessageAsync(
                        chatId: chatId,
                        text: listHtml,
                        parseMode: ParseMode.Html,
                        replyMarkup: replyKeyboardMarkup
                    );
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
            return totalN > index + CONTLIST_PAGE_SIZE;

        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            if (await ShowPageAsync(bot, 0, cancelationToken))
                return DialogResult.Handled;
            return DialogResult.Terminated;
        }

        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            if(callBack.Data.IndexOf(PAGE_PREFIX)==0)
            {
                if (await ShowPageAsync(bot, pageIndex, cancelationToken))
                    return DialogResult.Handled;
                return DialogResult.Terminated;
            }

            return DialogResult.Continue;
        }
    }
}
