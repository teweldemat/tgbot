using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.SmartLedger
{
    public class SetupFlowDialog:FormDialog
    {
        const string FIELD_CHECKER_ONE = "CheckerOne";
        const string FIELD_APPROVE_ONE = "ApproveOne";
        const string FIELD_ACCOUNTANT = "Accountant";
        

        public SetupFlowDialog(ChatId chatId, User from) : base(chatId, from)
        {

        }
        public override string FirstField => FIELD_CHECKER_ONE;
        
        public override FormDialogField GetFieldDef(string key)
        {
            switch(key)
            {
                case FIELD_CHECKER_ONE:
                    return new FormDialogField
                    {
                        Prompt = "Whom would you like to check payments?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult(FIELD_APPROVE_ONE),
                        Choices=SetupCompanyDialog<SmartLedgerDb>.GetUserChoices()
                    };
                case FIELD_APPROVE_ONE:
                    return new FormDialogField
                    {
                        Prompt = "Whom do you want to approve payments?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult(FIELD_ACCOUNTANT),
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices()
                    };
                case FIELD_ACCOUNTANT:
                    return new FormDialogField
                    {
                        Prompt = "Who is the accountant?",
                        FieldType = FieldType.Choices,
                        NextField = null,
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices()
                    };
            }
            return null;
        }

        
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();
            try
            {
                var e = service.GetEntity();
                if (e == null)
                {
                    await bot.SendTextMessageAsync(chatId, "Sorry the setup couldn't be applied because company is not setup");
                    return DialogResult.Terminated;
                }
                var oldConfigData = service.GetRule();
                var oldConfig = oldConfigData == null || oldConfigData.Rule==null? null : Newtonsoft.Json.JsonConvert.DeserializeObject<SimplePaymentFlowConfiguration>(oldConfigData.Rule);

                var config = new SimplePaymentFlowConfiguration
                {
                    Checker1 = (string)FieldData[FIELD_CHECKER_ONE].Val(),
                    Approver1 = (string)FieldData[FIELD_APPROVE_ONE].Val(),
                    Accountant= (string)FieldData[FIELD_ACCOUNTANT].Val(),
                };
                //notify the users assinged in the workflow
                if(config.Checker1!=null && (oldConfig == null || !config.Checker1.Equals(oldConfig.Checker1)))
                    await NotifyCheckerAssignment(bot, config.Checker1,cancellationToken);
                

                if (config.Approver1!= null && (oldConfig == null || !config.Approver1.Equals(oldConfig.Approver1)))
                    await NotifyApproverAssignment(bot, config.Approver1, cancellationToken);
                
                
                if (config.Accountant != null && (oldConfig == null || !config.Accountant.Equals(oldConfig.Accountant)))
                    await NotifyAccountatAssignment(bot, config.Accountant, cancellationToken);

                service.SetRule(from.Id.ToString(),null,Newtonsoft.Json.JsonConvert.SerializeObject(config));
                var cashAccount = service.AccountsCount();
                await SmartLedgerBot.RestartAsync(bot, chatId, from,
                    "The rules are updated."
                    +(cashAccount==0?"\nNow you need add cash accounts to start processing payments.":"")
                    , cancellationToken);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to register a setup", ex);
                await bot.SendTextMessageAsync(chatId, "Sorry the setup couldn't be applied because internal error. Please try again later");
            }
            return DialogResult.Terminated;
        }

        public static async Task NotifyCheckerAssignment(ITelegramBotClient bot, String checker
            ,CancellationToken cancellationToken)
        {
            var text = "You have been assigned as payment checker."
                                    //+ "\nAll payments will be forwared to you for checking. You will have power to accept or rejects the the requests based on your assesment of the requests"
                                    //+ "\nPayments you have accepted will be forwared to the approver"
                                    ;
            await bot.SendTextMessageAsync(chatId: checker, text: text
                                    );
            var profile = new SmartLedgerService().GetUserProfile(checker);
            var textGroup = $"{profile.FullName} have been assigned as payment checker.";
            await SmartLedgerBot.NotifyGroups(bot, textGroup,false,cancellationToken);
        }
        public static async Task NotifyAccountatAssignment(ITelegramBotClient bot, String accountant, 
            CancellationToken cancellationToken)
        {
            await bot.SendTextMessageAsync(chatId: accountant, text: "You have been assigned as payment accountant."
                                   // + "\nPayment that are approved and paid will be forwarded to you"
                                    //+ "\nYou will need to analyze the payment and post the transaction for accounting."
                                    //+ "\nYou will need to attach the copy of any docuemtn such as receipt and payment vouchers."
                                    //+"\nOnce you complete the accounting work, the payment is fowareded to approvers for closure"
                                    );
            var profile = new SmartLedgerService().GetUserProfile(accountant);
            var textGroup = $"{profile.FullName} have been assigned as payment accountant.";
            await SmartLedgerBot.NotifyGroups(bot, textGroup, false, cancellationToken);
        }
        public static async Task NotifyApproverAssignment(ITelegramBotClient bot, String accountant,
            CancellationToken cancellationToken)
        {
            await bot.SendTextMessageAsync(chatId: accountant, text: "You are assigned as payment approver."
                                    //+ "\nrequests will be forwared to you after they are checked. You will have power to accept or rejects the the requests based on your assesment of the requests"
                                    //+ "\nPayments you have accepted will be forwared for actual payment to payers"
                                    );
            var profile = new SmartLedgerService().GetUserProfile(accountant);
            var textGroup = $"{profile.FullName} have been assigned as payment approver.";
            await SmartLedgerBot.NotifyGroups(bot, textGroup, false, cancellationToken);
        }
        public static async Task NotifyPayerAssignment(ITelegramBotClient bot, String payer,String replace,bool deposit,Guid cashAccount,
            CancellationToken cancellationToken)
        {
            var workType = deposit ? "depositor" : "payer";
            var service = new SmartLedgerService();
            var profile = service.GetUserProfile(payer);
            var account = service.GetCashAccount(cashAccount);
            await bot.SendTextMessageAsync(chatId: payer, text: $"You are assigned as {workType} for {account.Name}"
                +(replace==null?"":$" replacing the {replace}")
                                    //+ "\nrequests will be forwared to you after they are approved. You will need to carefull check the request and make the actual payment"
                                    );
            var textGroup = $"{profile.FullName} is assigned as {workType} for {account.Name} "
                + (replace == null ? "" : $" replacing {replace}");
            await SmartLedgerBot.NotifyGroups(bot, textGroup, false, cancellationToken);
        }
    }
}
