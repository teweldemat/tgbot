using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.SmartLedger.AccountReconciliation
{
    public class AccountReconciliationDialog : FormDialog
    {

        const string FIELD_BALANCE = "Balance";
        const string FIELD_REMARK = "Remark";
        const string FIELD_ATTACHMENT_PREFIX = "Attachment";
        const string MORE_ATTACHMENT_PREFIX = "More Attachment";

        public long Balance() => (long)FieldData[FIELD_BALANCE].Val();
        public String Remark() => (string)base.FieldData[FIELD_REMARK].Val();
        public Guid AccountId;
        SmartLedgerService service;
        TgDbService tgService;
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<SmartLedgerService>();
            this.tgService = services.GetService<TgDbService>();
        }
        public AccountReconciliationDialog(SmartLedgerService service,TgDbService tgService, ChatId chatId, User from, Guid accountId) : base(chatId, from)
        {
            this.AccountId = accountId;
            this.service = service;
            this.tgService = tgService;
        }
        public void SetServices(SmartLedgerService service, TgDbService tgService)
        {
            this.service = service;
            this.tgService = tgService;
        }
        public override string FirstField => FIELD_BALANCE;
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var guid = service.CreateReconciliationFlow(
               from.Id.ToString(),
               this.Remark(),
               this.AccountId,
               this.Balance(),
               service.GetCashAccount(this.AccountId).Balance,
               this.Pictures(FIELD_ATTACHMENT_PREFIX).Select(x => new WorkItemPicture
               {
                   Image = x.Image,
                   ImgeMime = x.ImageMime,
                   LinkedImage = x.ContentLink,
                   LinkedImageType = "1"
               }).ToList()
           );

            var econciliation = service.GetReconciliation(guid);
            try
            {
                await bot.SendTextMessageAsync(chatId, "Account reconciliation request is registered.");
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error sending confirmation for account reconciliation request", ex);
            }
            try
            {
                var state = tgService.GetUserState(from.Id.ToString());
                var account = service.GetCashAccount(this.AccountId);

                await SmartLedgerBot.NotifyGroups(bot,tgService, $"{TGBot.FullName(from)} requested balance of account {account.Name} to be reset to {System.Web.HttpUtility.HtmlEncode(IntData.toString(this.Balance()))} Birr"
                    + $"\n {SmartLedgerBot.ReconciliationLink(econciliation.Id, econciliation.Reference)}", true, cancellationToken);
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                var e = service.GetEntity();
                if (e?.Owner!=null)
                {
                    var user = new User();
                    user.Id = long.Parse(e?.Owner);
                    user.FirstName = "Unknown";
                    await TGBot.PushDialog(e?.Owner, new ReconciliationDetailDialog(service, e?.Owner, user, guid), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error notifying groups and checkers of creation", ex);
            }
            return DialogResult.Terminated;
        }



        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_BALANCE:
                    return new FormDialogField
                    {
                        Prompt = "What is the balance of the account?",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_REMARK),
                        ParseFunction = (bot, t, c) =>
                        {
                            if (double.TryParse(t, out var d))
                            {
                                if (d > 0)
                                {
                                    return Task.FromResult(new ParseResult
                                    {
                                        Data = IntData.toIntMoney(d)
                                    });
                                }
                            }
                            return Task.FromResult(new ParseResult
                            {
                                Error = "Invalid amount"
                            });
                        }

                    };
                case FIELD_REMARK:
                    return new FormDialogField
                    {
                        Prompt = "Enter remark",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_ATTACHMENT_PREFIX + "0")
                    };
            }
            if (key.StartsWith(FIELD_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_ATTACHMENT_PREFIX.Length));
                return new FormDialogField
                {
                    Prompt = "Upload an attachment for your request",
                    FieldType = FieldType.Attachment,
                    NextField = d => Task.FromResult(MORE_ATTACHMENT_PREFIX + index)
                };
            }
            if (key.StartsWith(MORE_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(MORE_ATTACHMENT_PREFIX.Length));
                return new FormDialogField
                {
                    Prompt = "Do you want to add more attachment?",
                    FieldType = FieldType.Choices,
                    Choices = new[] { new FormFieldChoiceItem("YES"), new("NO") },
                    NextField = d =>
                    {
                        var ret = Task.FromResult("YES".Equals(d[key].Val()) ? FIELD_ATTACHMENT_PREFIX + (index + 1) : null);
                        return ret;
                    }
                };
            }
            return null;
        }
    }
}
