using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    public class ApproveAcceptDialog : FormDialog
    {
        const string FIELD_NOTE = "Note";

        //persitent
        public Guid PaymentId { get; set; }
        public int ApproveType { get; set; }
        public bool SendBankToAccount { get; set; }

        //transiet
        SmartLedgerService service;
        TgDbService tgService;
        Payment payment { get; set; }

        void init()
        {
            payment = service.GetPayment(PaymentId);
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new UserFriendlyError("Configuration not set");

        }
        public ApproveAcceptDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId, int approveType = PaymentWorkItem.WORK_TYPE_APPROVE) : base(chatId, from)
        {
            this.service = service;
            this.tgService = tgService;
            ApproveType = approveType;
            PaymentId = paymentId;
            if (this.service != null)
                init();
        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
            init();
        }
        public override string FirstField => FIELD_NOTE;

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = "Please enter some remark",
                        FieldType = FieldType.Text,
                        NextField = null,
                    };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var state = tgService.GetUserState(from.Id.ToString());
            var prof = service.GetUserProfile(from.Id.ToString());

            service.AddPaymentWorkItem(
                userId: from.Id.ToString(),
                work: new PaymentWorkItem
                {
                    PaymentId = payment.Id,
                    WorkType = ApproveType,
                    Note = (string)FieldData[FIELD_NOTE].Val(),
                });
            string message;
            string messageGroup = null;
            string messageOwner = null;
            try
            {

                switch (ApproveType)
                {
                    case PaymentWorkItem.WORK_TYPE_CLOSE:
                        message = "Thank you for closing the request.";
                        messageGroup = $"{prof.FullName} closed request "
                                + $"{SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
                        messageOwner = $"{prof.FullName} closed your request /{payment.Reference}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING:
                        message = "Request sent back to accounting.";
                        messageGroup = $"{prof.FullName} sent request back to accounting"
                                + $"{SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
                        messageOwner = $"{prof.FullName} sent your request back to accounting /{payment.Reference}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT:
                        message = "Request sent back to payment.";
                        messageGroup = $"{prof.FullName} sent request back to payment"
                                + $"{SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
                        messageOwner = $"{prof.FullName} sent your request back to payment /{payment.Reference}";
                        break;
                    default:
                        message = "Thank you for approving the request.";
                        messageGroup = $"{prof.FullName} approved request {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
                        messageOwner = $"{prof.FullName} approved your request /{payment.Reference}";
                        break;
                }

                await bot.SendTextMessageAsync(chatId, message);
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error send confirmation for approval", ex);
            }
            try
            {

                //notify group
                if (messageGroup != null)
                    await SmartLedgerBot.NotifyGroups(bot, tgService, messageGroup, true, cancellationToken);

                //notify creator
                if (messageOwner != null)
                    await bot.SendTextMessageAsync(chatId: payment.Creator,
                            text: messageOwner,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);

                //notify next stage
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                if (config != null)
                {

                    switch (ApproveType)
                    {
                        case PaymentWorkItem.WORK_TYPE_APPROVE:
                        case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT:
                            foreach (var source in service.GetPaymentSources(payment.Id))
                            {
                                var payer = config.GetPayer(source.CashAccountId, payment.IsDeposit);
                                if (payer == null)
                                    throw new UserFriendlyError("Payers are not fully configured");
                                var user = new User();
                                user.Id = long.Parse(payer);
                                user.FirstName = "Unknown";
                                await TGBot.PushDialog(payer, new PaymentDetailDialog(service, tgService, payer, user, payment.Id), cancellationToken);
                            }
                            break;

                        case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING:
                            {
                                var user = new User();
                                user.Id = long.Parse(config.Accountant);
                                user.FirstName = "Unknown";
                                await TGBot.PushDialog(config.Accountant, new PaymentDetailDialog(service, tgService, config.Accountant, user, payment.Id), cancellationToken);
                            }
                            break;
                        case PaymentWorkItem.WORK_TYPE_CLOSE:
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error notifying groups and relevant users", ex);
            }
            return DialogResult.Terminated;
        }
    }
}
