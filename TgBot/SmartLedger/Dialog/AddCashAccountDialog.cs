using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    public class AddCashAccountDialog : FormDialog
    {
        const string FIELD_ACOUNT_NAME = "AccountName";
        const string FIELD_ACOUNT_HAS_CODE = "HasCode";
        const string FIELD_ACOUNT_BANK_ACCOUNT_CODE = "AccountCode";
        const string FIELD_ACOUNT_BALANCE = "AccountBalance";
        const string FIELD_ACOUNT_PAYER = "AccountPayer";
        const string FIELD_ACOUNT_DEPOSITOR = "AccountDepositor";

        SmartLedgerService service;
        TgDbService tgService;

        public AddCashAccountDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from) : base(chatId, from)
        {
            this.service = service;
            this.tgService = tgService;
        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
        }

        public override string FirstField => FIELD_ACOUNT_NAME;
        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_ACOUNT_NAME:
                    return new FormDialogField
                    {
                        Prompt = "Enter the name of the account",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_ACOUNT_HAS_CODE),
                        ParseFunction = (bot, t, c) =>
                        {
                            if (string.IsNullOrWhiteSpace(t))
                                return Task.FromResult(new ParseResult { Error = "Enter valid name" });
                            return Task.FromResult(new ParseResult { Data = t });
                        }
                    };
                case FIELD_ACOUNT_HAS_CODE:
                    return new FormDialogField
                    {
                        Prompt = "What type of account is this?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult("BANK".Equals(d[FIELD_ACOUNT_HAS_CODE].Val()) ? FIELD_ACOUNT_BANK_ACCOUNT_CODE : FIELD_ACOUNT_BALANCE),
                        Choices = new[] { new FormFieldChoiceItem("BANK"), new("Cash On Hand") }
                    };
                case FIELD_ACOUNT_BANK_ACCOUNT_CODE:
                    return new FormDialogField
                    {
                        Prompt = "Enter account code",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_ACOUNT_BALANCE),
                        ParseFunction = (bot, t, c) =>
                        {
                            if (string.IsNullOrWhiteSpace(t))
                                return Task.FromResult(new ParseResult { Error = "Enter valid account code" });
                            return Task.FromResult(new ParseResult { Data = t });
                        }
                    };
                case FIELD_ACOUNT_BALANCE:
                    return new FormDialogField
                    {
                        Prompt = "Enter the current balance of the account",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_ACOUNT_PAYER),
                        ParseFunction = (bot, text, cancelationToken) =>
                        {
                            if (double.TryParse(text, out var v))
                            {
                                if (v < 0)
                                    return Task.FromResult(new ParseResult { Error = "Negative value not allowed." });
                                return Task.FromResult(new ParseResult { Data = IntData.toIntMoney(v) });
                            }
                            return Task.FromResult(new ParseResult { Error = "Enter just the amount." });
                        }

                    };
                case FIELD_ACOUNT_PAYER:
                    return new FormDialogField
                    {
                        Prompt = "Who makes payments for this account?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult(FIELD_ACOUNT_DEPOSITOR),
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices(service)
                    };
                case FIELD_ACOUNT_DEPOSITOR:
                    return new FormDialogField
                    {
                        Prompt = "Who makes deposits to this account?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult<string>(null),
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices(service)
                    };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var e = service.GetEntity();
            var userId = from.Id.ToString();
            if (!e.Owner.Equals(userId))
            {
                await bot.SendTextMessageAsync(chatId, "Sorry, only owner can add accounts");
                return DialogResult.Terminated;
            }
            var configData = service.GetRule();
            if (configData == null || configData.Rule == null)
            {
                await bot.SendTextMessageAsync(chatId, "Sorry, rule configuration could't be loaded");
                return DialogResult.Terminated;
            }
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<SimplePaymentFlowConfiguration>(configData.Rule);
            if (config == null)
            {
                await bot.SendTextMessageAsync(chatId, "Sorry, rule configuration could't be loaded");
                return DialogResult.Terminated;
            }
            try
            {
                var cashAccountId = service.AddCashAccount(userId, new CashAccount
                {
                    Balance = (long)FieldData[FIELD_ACOUNT_BALANCE].Val(),
                    Code = "BANK".Equals(FieldData[FIELD_ACOUNT_HAS_CODE].Val()) ? (string)FieldData[FIELD_ACOUNT_BANK_ACCOUNT_CODE].Val() : null,
                    Name = (string)FieldData[FIELD_ACOUNT_NAME].Val()
                }, id =>
                 {
                     if (config.Payers == null)
                         config.Payers = new List<SimplePaymentFlowConfiguration.AccountPayer>();
                     config.Payers.Add(new SimplePaymentFlowConfiguration.AccountPayer
                     {
                         AccountId = id,
                         Payer = (string)FieldData[FIELD_ACOUNT_PAYER].Val(),
                         Depositor = (string)FieldData[FIELD_ACOUNT_DEPOSITOR].Val()
                     });
                     return new PaymentFlowRule
                     {

                         Rule = Newtonsoft.Json.JsonConvert.SerializeObject(config),
                         RuleType = null
                     };
                 });

                await SmartLedgerBot.RestartAsync(bot, chatId, from, "The account is added", cancellationToken);
                await SetupFlowDialog.NotifyPayerAssignment(bot, service, tgService, (string)FieldData[FIELD_ACOUNT_PAYER].Val(), null, false, cashAccountId, cancellationToken);
                await SetupFlowDialog.NotifyPayerAssignment(bot, service, tgService, (string)FieldData[FIELD_ACOUNT_DEPOSITOR].Val(), null, true, cashAccountId, cancellationToken);
                return DialogResult.Terminated;
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to add account", ex);
                await bot.SendTextMessageAsync(chatId, "The account couldn't be added becaues of internal error. Try again latter");
                return DialogResult.Terminated;
            }
        }
    }
}
