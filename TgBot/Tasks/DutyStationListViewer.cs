using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.Tasks
{
    class DutyStationListViewer :BotDialogBase
    {
        public ChatId chatId;
        public User user;
        public DutyStation SelectedDutyStation { get; set; }
        public List<DutyStation> DutyStations { get; set; }
        public DutyStationListViewer()
        {
        }
        public DutyStationListViewer(ChatId chatId, User user)
        {
            this.chatId = chatId;
            this.user = user;
        }
        public static string FormatDutyStation(TaskDbService coreService, DutyStation dutyStation,String numLabel)
        {

            var html = $"{numLabel} {dutyStation.Name} ({dutyStation.Address})";
            return html;
        }
        public String FormatDutyStationDetail(DutyStation dutyStation)
        {
            var service = new TaskDbService();
            var count=service.DutyStationWorkerCount(dutyStation.Id);
            var ds = service.GetDutyStation(dutyStation.Id);
            var html = 
$@"Duty station name:{ds.Name}<pre>
</pre>Code: {ds.Code}<pre>
</pre>Address: {ds.Address})<pre>
</pre>Team memebers assigned: {(count==0?"none":count.ToString())}";
            return html;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            /*if(this.ModifyDialog!=null)
            {
                switch(await this.ModifyDialog.HandleCallBackAsync(bot, callBack, cancelationToken))
                {
                    case DialogResult.Handled:
                        return DialogResult.Handled;
                    case DialogResult.Terminated:
                        this.ModifyDialog = null;
                        this.SelectedAccount = new SmartLedgerService().GetCashAccount(this.SelectedAccount.Id);
                        await DisplayAccountDetail(bot);
                        return DialogResult.Handled;
                }
            }*/
            if("Delete".Equals(callBack.Data))
            {
                return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            
            DutyStation ds=DutyStations.Where(x=>x.Code.ToLower().Equals(message.Text.ToLower())).FirstOrDefault();
            if (ds != null)
            {
                this.SelectedDutyStation = ds;
                await DisplayDutyStationDetail(bot);
            }
            return DialogResult.Continue;
        }

        private async Task DisplayDutyStationDetail(ITelegramBotClient bot)
        {
            var e = new TaskDbService().GetEntity();
            if (e != null && e.Owner.Equals(user.Id.ToString()))
            {

                await bot.SendTextMessageAsync(chatId,
                    text: FormatDutyStationDetail(this.SelectedDutyStation),
                    parseMode: ParseMode.Html,
                    replyMarkup: FormDialog.CreateInlineButtons(new KeyValuePair<String,String>[] { 
                        //new KeyValuePair<String, String>("Delete", "Delete")
                        })
                    );
            }
            else
            {
                await bot.SendTextMessageAsync(chatId,
                text: FormatDutyStationDetail(this.SelectedDutyStation),
                parseMode: ParseMode.Html);
            }
        }

        async Task ShowPageAsync(ITelegramBotClient bot,int index, CancellationToken cancelationToken)
        {
            
            var coreService = new TaskDbService();
            DutyStations= coreService.GetAllDutyStations();
            if (DutyStations.Count == 0)
            {
                await bot.SendTextMessageAsync(chatId, "No duty station created");
            }
            else
            {
                var n = 1;
                String listHtml = null;
                foreach (var f in DutyStations)
                {
                    var html = FormatDutyStation(coreService, f, $"{f.Code}: ");
                    listHtml = listHtml == null ? html : (listHtml + "<pre>\n</pre>" + html);
                    n++;
                }
                listHtml += "<pre>\n</pre>Enter duty station code to see detail";
                await bot.SendTextMessageAsync(chatId,
                    text: listHtml,
                    parseMode: ParseMode.Html);

            }
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await ShowPageAsync(bot, 0, cancelationToken);
            return DialogResult.Handled;
        }        
    }
}
