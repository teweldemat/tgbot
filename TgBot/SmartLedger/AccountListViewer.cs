using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.SmartLedger
{
    class AccountListViewer :BotDialogBase
    {
        public ChatId chatId;
        public User user;
        public ModifyCashAccountDialog ModifyDialog { get; set; } = null;
        public CashAccount SelectedAccount { get; set; }
        public List<CashAccount> Accounts { get; set; }
        public AccountListViewer(ChatId chatId, User user)
        {
            this.chatId = chatId;
            this.user = user;
        }
        public static string FormatAccount(SmartLedgerService coreService, CashAccount account,String numLabel)
        {

            var html = $"{numLabel} {account.Name} {IntData.toString(account.Balance)} Birr";
            return html;
        }
        public String FormatAccountDetail(CashAccount account)
        {
            var service = new SmartLedgerService();
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            String payer = null;
            String depositor = null;
            if (config != null)
            {
                payer = config.GetPayer(account.Id, false);
                depositor = config.GetPayer(account.Id, true);
            }


            if (payer == null)
            {
                payer = "Payer not set";
            }
            else
            {
                payer = service.GetUserProfile(payer).FullName;
            }

            if (depositor == null)
            {
                depositor = "Depositor not set";
            }
            else
            {
                depositor = service.GetUserProfile(depositor).FullName;
            }

            var ac = service.GetCashAccount(account.Id);
            var html = $"<strong>Accout Name:</strong> {ac.Name}"
                    + $"<pre>\n</pre><strong>Account Type:</strong> {(String.IsNullOrEmpty(ac.Code) ? "Cash on Hand" : "Bank")}"
                    + $"<pre>\n</pre><strong>Balance:</strong> {IntData.toString(ac.Balance)}"
                    + $"<pre>\n</pre><strong>Payer:</strong> { payer}"
                    + $"<pre>\n</pre><strong>Depositor:</strong> { depositor}"
                    + (account.Code==null?"":$"<pre>\n</pre><strong>Account #:</strong> {account.Code}")
                    + $"<pre>\n</pre><a href=\"{SmartLedgerBot.GetLedgerLink(ac.Id)}\">Ledger</a>"
                    ;
            return html;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            if(this.ModifyDialog!=null)
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
            }
            if("Modify".Equals(callBack.Data))
            {
                this.ModifyDialog = new ModifyCashAccountDialog(this.chatId, this.user, this.SelectedAccount.Id);
                await this.ModifyDialog.StartAsync(bot, cancelationToken);
                return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            if (this.ModifyDialog != null)
            {
                switch (await this.ModifyDialog.HandleMessageAsync(bot, message, cancelationToken))
                {
                    case DialogResult.Handled:
                        return DialogResult.Handled;
                    case DialogResult.Terminated:
                        this.ModifyDialog = null;
                        this.SelectedAccount = new SmartLedgerService().GetCashAccount(this.SelectedAccount.Id);
                        await DisplayAccountDetail(bot);
                        return DialogResult.Handled;
                }
            }
            int n;
            if (int.TryParse(message.Text.Trim(), out n))
            {
                if(n>=1 && n<=Accounts.Count)
                {


                    this.SelectedAccount = this.Accounts[n - 1];
                    await DisplayAccountDetail(bot);
                }
            }
            return DialogResult.Continue;
        }

        private async Task DisplayAccountDetail(ITelegramBotClient bot)
        {
            var e = new SmartLedgerService().GetEntity();
            if (e != null && e.Owner.Equals(user.Id.ToString()))
            {

                await bot.SendTextMessageAsync(chatId,
                    text: FormatAccountDetail(this.SelectedAccount),
                    parseMode: ParseMode.Html,
                    replyMarkup: FormDialog.CreateInlineButtons(new[] { new KeyValuePair<String, String>("Modify", "Modify") })
                    );

            }
            else
            {
                await bot.SendTextMessageAsync(chatId,
                text: FormatAccountDetail(this.SelectedAccount),
                parseMode: ParseMode.Html);
            }
        }

        async Task ShowPageAsync(ITelegramBotClient bot,int index, CancellationToken cancelationToken)
        {
            
            var coreService = new SmartLedgerService();
            Accounts= coreService.GetCashAccounts();
            if (Accounts.Count == 0)
            {
                await bot.SendTextMessageAsync(chatId, "No account created");
            }
            else
            {
                var n = 1;
                String listHtml = null;
                foreach (var f in Accounts)
                {
                    var html = FormatAccount(coreService, f, $"{index + n}. ");
                    listHtml = listHtml == null ? html : (listHtml + "<pre>\n</pre>" + html);
                    n++;
                }
                listHtml += "<pre>\n</pre>Enter a numer to see details";
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
