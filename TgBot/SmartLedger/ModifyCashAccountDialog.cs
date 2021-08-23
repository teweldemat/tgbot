using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.SmartLedger
{
    public class ModifyCashAccountDialog : FormDialog
    {
        enum ModifyType
        {
            SetPayer,
            SetDepositor,
            SetName,
            ChangeType,
        }
        const string FIELD_TYPE = "Type";
        const string FIELD_DATA = "String";
        public Guid CashAccountId { get; set; }
        public override string FirstField => FIELD_TYPE;
        public ModifyCashAccountDialog(ChatId chatId,User from,Guid cahsAccountId)
            :base(chatId,from)
        {
            this.CashAccountId = cahsAccountId;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();
            var account = service.GetCashAccount(this.CashAccountId);
            var oldConfig = service.GetRuleData<SimplePaymentFlowConfiguration>();
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new UserFriendlyError("Existing configuration not found");

            bool accountUpdated = false;
            bool configUpdated = false;
            try
            {
                MisUserProfile notifyPayer = null;
                String oldPayer = null;
                bool deposit = false;
                switch ((ModifyType)FieldData[FIELD_TYPE].Val())
                {
                    case ModifyType.SetPayer:
                        configUpdated = true;
                        var payer = (string)FieldData[FIELD_DATA].Val();
                        config.SetPayer(this.CashAccountId, false, payer);
                        notifyPayer = service.GetUserProfile(payer);
                        oldPayer = oldConfig.GetPayer(this.CashAccountId, false);
                        break;
                    case ModifyType.SetDepositor:
                        configUpdated = true;
                        var depositor = (string)FieldData[FIELD_DATA].Val();
                        config.SetPayer(this.CashAccountId, true, depositor);
                        notifyPayer = service.GetUserProfile(depositor);
                        oldPayer = oldConfig.GetPayer(this.CashAccountId, true);
                        deposit = true;
                        break;
                    case ModifyType.SetName:
                        accountUpdated = true;
                        account.Name = (string)FieldData[FIELD_DATA].Val();
                        break;
                    case ModifyType.ChangeType:
                        accountUpdated = true;
                        account.Code = account.Code == null ? (string)FieldData[FIELD_DATA].Val() : null;
                        break;
                    default:
                        break;
                }
                if (accountUpdated)
                    service.UpdateAccount(from.Id.ToString(), account, null, configUpdated ? Newtonsoft.Json.JsonConvert.ToString(config) : null);
                else if (configUpdated)
                {
                    service.SetRule(from.Id.ToString(), null, Newtonsoft.Json.JsonConvert.SerializeObject(config));
                    try
                    {
                        if (notifyPayer != null)
                            await SetupFlowDialog.NotifyPayerAssignment(bot, notifyPayer.UserId,
                                oldPayer == null ? null : service.GetUserProfile(oldPayer).FullName, deposit, this.CashAccountId, cancellationToken);

                    }
                    catch(Exception ex)
                    {
                        Program.LogException("Error notifying groups and users", ex);
                    }
                }
                return DialogResult.Terminated;
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to add account", ex);
                await bot.SendTextMessageAsync(chatId, "The account couldn't be added becaues of internal error. Try again latter");
                return DialogResult.Terminated;
            }
        }
        public override FormDialogField GetFieldDef(string key)
        {
            var service = new SmartLedgerService();
            var account = service.GetCashAccount(this.CashAccountId);
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new UserFriendlyError("Existing configuration not found");
            switch (key)
            {
                case FIELD_TYPE:
                    return new FormDialogField
                    {
                        Prompt = "What do you want to change?",
                        FieldType = FieldType.Choices,
                        Choices = new[] {
                            new FormFieldChoiceItem(ModifyType.SetName.ToString(), "Change Name"),
                            new FormFieldChoiceItem(ModifyType.SetPayer.ToString(), "Change Payer"),
                            new FormFieldChoiceItem(ModifyType.SetDepositor.ToString(), "Change Deposior"),
                            new FormFieldChoiceItem(ModifyType.ChangeType.ToString(), account.Code == null ? "Make Bank Account" : "Make Cash Account"),
                        },
                        NextField = d => {
                            if ((ModifyType)d[FIELD_TYPE].Val() == ModifyType.ChangeType && account.Code != null)
                                return Task.FromResult<String>(null);
                            return Task.FromResult(FIELD_DATA);
                            },
                        ParseFunction = (b, t, c) => Task.FromResult(new ParseResult { Data= Enum.Parse<ModifyType>(t) }),
                    };
                case FIELD_DATA:
                    String prompt;
                    FieldType fieldType;
                    IList<FormFieldChoiceItem> choices = null;
                    switch ((ModifyType)FieldData[FIELD_TYPE].Val())
                    {
                        case ModifyType.SetPayer:
                            prompt = "Select new payer:";
                            fieldType = FieldType.Choices;
                            choices = service.GetAllUserProfiles()
                                .Where(x => !config.IsPayer(x.UserId, this.CashAccountId, false))
                                .Select(x => new FormFieldChoiceItem(x.UserId, x.FullName)).ToList();
                            break;
                        case ModifyType.SetDepositor:
                            prompt = "Select new depositor:";
                            fieldType = FieldType.Choices;
                            choices = service.GetAllUserProfiles()
                                .Where(x => !config.IsPayer(x.UserId, this.CashAccountId, true))
                                .Select(x => new FormFieldChoiceItem(x.UserId, x.FullName)).ToList();
                            break;
                        case ModifyType.SetName:
                            prompt = "Enter new new for the account:";
                            fieldType = FieldType.Text;
                            break;
                        case ModifyType.ChangeType:
                            prompt = "Enter bank account code:";
                            fieldType = FieldType.Text;
                            break;
                        default:
                            fieldType = FieldType.Text;
                            prompt = null;
                            break;
                    }

                    return new FormDialogField
                    {
                        FieldType=fieldType,
                        Prompt=prompt,
                        Choices = choices,
                        NextField=null,
                    };

            }
            return null;
        }
    }
}
