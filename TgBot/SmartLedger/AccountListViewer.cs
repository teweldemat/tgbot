using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.SmartLedger.AccountReconciliation;
using TgBot.SmartLedger.Dialog;
using TgBot.TgDb;

namespace TgBot.SmartLedger
{
    class AccountListViewer : BotDialogBase
    {
        public ChatId chatId;
        public User user;
        public ModifyCashAccountDialog ModifyDialog { get; set; } = null;
        public CashAccount SelectedAccount { get; set; }
        public List<CashAccount> Accounts { get; set; }
        SmartLedgerService service;
        TgDbService tgService;
        public AccountListViewer(SmartLedgerService service,TgDbService tgService, ChatId chatId, User user)
        {
            this.chatId = chatId;
            this.user = user; 
            this.service = service;
            this.tgService = tgService;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<SmartLedgerService>();
            this.tgService = services.GetService<TgDbService>();
            if(this.ModifyDialog!=null)
                this.ModifyDialog.SetServices(services);    
        }
        public static string FormatAccount(SmartLedgerService coreService, CashAccount account, String numLabel)
        {

            var html = $"/{numLabel} <strong>{account.Name}</strong> {IntData.toString(account.Balance)} Birr";
            return html;
        }
        public String FormatAccountDetail(CashAccount account)
        {
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
            var html = $"<strong>Account Name:</strong> {ac.Name}"
                    + $"\n<strong>Account Type:</strong> {(String.IsNullOrEmpty(ac.Code) ? "Cash on Hand" : "Bank")}"
                    + $"\n<strong>Balance:</strong> {IntData.toString(ac.Balance)}"
                    + $"\n<strong>Payer:</strong> {payer}"
                    + $"\n<strong>Depositor:</strong> {depositor}"
                    + (account.Code == null ? "" : $"\n<strong>Account #:</strong> <u>{account.Code}</u>")
                    + $"\n<a href=\"{SmartLedgerBot.GetLedgerLink(ac.Id)}\">Ledger</a>"
                    ;
            return html;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancellationToken)
        {
            if (this.ModifyDialog != null)
            {
                switch (await this.ModifyDialog.HandleCallBackAsync(bot, callBack, cancellationToken))
                {
                    case DialogResult.Handled:
                        return DialogResult.Handled;
                    case DialogResult.Terminated:
                        this.ModifyDialog = null;
                        this.SelectedAccount = service.GetCashAccount(this.SelectedAccount.Id);
                        await DisplayAccountDetail(bot);
                        return DialogResult.Handled;
                }
            }
            switch (callBack.Data)
            {
                case "Modify":
                    this.ModifyDialog = new ModifyCashAccountDialog(service,tgService,this.chatId, this.user, this.SelectedAccount.Id);
                    await this.ModifyDialog.StartAsync(bot, cancellationToken);
                    return DialogResult.Handled;
                case "Reconcile":
                    await TGBot.PushDialog(this.user.Id.ToString(), new AccountReconciliationDialog(service,tgService, this.chatId, this.user, this.SelectedAccount.Id), cancellationToken);
                    break;
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
                        this.SelectedAccount = service.GetCashAccount(this.SelectedAccount.Id);
                        await DisplayAccountDetail(bot);
                        return DialogResult.Handled;
                }
            }
            int n;
            
            if (int.TryParse(message.Text.Trim().TrimStart('/'), out n))
            {
                if (n >= 1 && n <= Accounts.Count)
                {
                    this.SelectedAccount = this.Accounts[n - 1];
                    await DisplayAccountDetail(bot);
                    return DialogResult.Handled;
                }
            }
            return DialogResult.Continue;
        }

        private async Task DisplayAccountDetail(ITelegramBotClient bot)
        {

            var e = service.GetEntity();
            var buttons = new List<InlineKeyboardButton[]>();
            if (e != null && e.Owner.Equals(user.Id.ToString()))
            {
                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Modify", "Modify") });
            }
            var conf = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (conf != null && conf.Accountant == user.Id.ToString())
            {
                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Reconcile", "Reconcile") });
            }
            var replyMarkup = new InlineKeyboardMarkup(buttons);
            await bot.SendTextMessageAsync(chatId,
                text: FormatAccountDetail(this.SelectedAccount),
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup);
        }

        async Task ShowPageAsync(ITelegramBotClient bot, int index, CancellationToken cancelationToken)
        {

            Accounts = service.GetCashAccounts();
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
                    var html = FormatAccount(service, f, $"{index + n}. ");
                    listHtml = listHtml == null ? html : (listHtml + "\n" + html);
                    n++;
                }
                listHtml += "\nEnter a numer to see details";
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
