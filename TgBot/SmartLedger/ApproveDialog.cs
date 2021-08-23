using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgBot.SmartLedger
{

    public class ApproveRejectDialog : RejectDialogBase
    {
        public bool ReversePayment { get; set; }
        protected override int WorkType => PaymentWorkItem.WORK_TYPE_CANCELED;

        protected override IEnumerable<string> NextStage => null;
        
        public ApproveRejectDialog(ChatId chatId, User from, Guid paymentId,bool reversePayment=false) : base(chatId, from, paymentId)
        {
            this.PaymentId = paymentId;
            this.ReversePayment = reversePayment;
        }
        protected override string GroupNotification(string rejecterName, Payment payment)
        {
            return $"{rejecterName} rejected request "
                    + $"{SmartLedgerBot.PaymentLink(payment.Id,payment.Reference)}";
        }

        protected override string OwnerNotification(string rejecterName, Payment payment)
        {
            return $"{rejecterName} rejected your request "
                                + $"/{payment.Reference}";
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();

            service.AddWorkItem(from.Id.ToString(),
                new PaymentWorkItem
                {
                    PaymentId = this.PaymentId,
                    WorkType = this.WorkType,
                    Note = (String)this.FieldData[FIELD_NOTE].Val()
                },reversePayment:this.ReversePayment);
            try
            {
                await bot.SendTextMessageAsync(chatId, "The request has been canceled.");
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error send confirmation for check reject", ex);
            }
            try
            {
                var tgService = new TgBot.TgDb.TgDbService();
                var state = tgService.GetUserState(from.Id.ToString());
                var prof = service.GetUserProfile(from.Id.ToString());
                var payment = service.GetPayment(PaymentId);

                //notify group
                var groupNotif = this.GroupNotification(prof.FullName, payment);
                if (groupNotif != null)
                    await SmartLedgerBot.NotifyGroups(bot, groupNotif, true, cancellationToken);

                //notify owner
                var ownerNotification = this.OwnerNotification(prof.FullName, payment);
                if (ownerNotification != null)
                    await bot.SendTextMessageAsync(chatId: payment.Creator,
                            text: ownerNotification,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);

                //notify next stage
                var next = this.NextStage;
                if (next != null)
                {
                    foreach (var v in next)
                    {
                        var user = new User();
                        user.Id = long.Parse(v);
                        user.FirstName = "Unknown";
                        await TGBot.PushDialog(v, new PaymentDetailDialog(user.Id, user, PaymentId), cancellationToken);
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
    public class ApproveAcceptDialog : FormDialog
    {
        const String FIELD_NOTE = "Note";
        public Payment payment { get; set; }
        public override string FirstField => FIELD_NOTE;
        public int ApproveType { get; set; }
        public bool SendBankToAccount { get; set; }
        public ApproveAcceptDialog(ChatId chatId, User from, Guid paymentId,int approveType=PaymentWorkItem.WORK_TYPE_APPROVE) : base(chatId, from)
        {
            var service = new SmartLedgerService();
            this.ApproveType = approveType;
            this.payment = service.GetPayment(paymentId);
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new UserFriendlyError("Configuration not set");
        }

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
            var service = new SmartLedgerService();
            var tgService = new TgBot.TgDb.TgDbService();
            var state = tgService.GetUserState(from.Id.ToString());
            var prof = service.GetUserProfile(from.Id.ToString());

            service.AddWorkItem(
                userId:from.Id.ToString(),
                work:new PaymentWorkItem
                {
                    PaymentId=this.payment.Id,
                    WorkType =ApproveType,
                    Note=(String)this.FieldData[FIELD_NOTE].Val(),
                });
            String message;
            String messageGroup=null;
            String messageOwner=null;
            try
            {
                
                switch(ApproveType)
                {
                    case PaymentWorkItem.WORK_TYPE_CLOSE:
                        message = "Thank you for closing the request.";
                        messageGroup = $"{prof.FullName} closed request "
                                + $"{SmartLedgerBot.PaymentLink(payment.Id,payment.Reference)}";
                        messageOwner = $"{prof.FullName} closed your request /{payment.Reference}";
                        break;
                    case PaymentWorkItem.WORK_TYPE_RESTART:
                        message = "Request restarted.";
                        messageGroup = $"{prof.FullName} restared request "
                                + $"{SmartLedgerBot.PaymentLink(payment.Id,payment.Reference)}";
                        messageOwner = $"{prof.FullName} restared your request /{payment.Reference}";
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
                        messageGroup = $"{prof.FullName} approved request {SmartLedgerBot.PaymentLink(payment.Id,payment.Reference)}";
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
                    await SmartLedgerBot.NotifyGroups(bot, messageGroup, true, cancellationToken);

                //notify creator
                if (messageOwner != null)
                    await bot.SendTextMessageAsync(chatId: payment.Creator,
                            text: messageOwner,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);

                //notify next stage
                var config = new SmartLedgerService().GetRuleData<SimplePaymentFlowConfiguration>();
                if (config != null)
                {

                    switch (this.ApproveType)
                    {
                        case PaymentWorkItem.WORK_TYPE_APPROVE:
                        case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT:
                            foreach (var source in service.GetPaymentSources(this.payment.Id))
                            {
                                var payer = config.GetPayer(source.CashAccountId, payment.IsDeposit);
                                if (payer == null)
                                    throw new UserFriendlyError("Payers are not fully configured");
                                var user = new User();
                                user.Id = long.Parse(payer);
                                user.FirstName = "Unknown";
                                await TGBot.PushDialog(payer, new PaymentDetailDialog(payer, user, payment.Id), cancellationToken);
                            }
                            break;

                        case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING:
                            {
                                var user = new User();
                                user.Id = long.Parse(config.Accountant);
                                user.FirstName = "Unknown";
                                await TGBot.PushDialog(config.Accountant, new PaymentDetailDialog(config.Accountant, user, payment.Id), cancellationToken);
                            }
                            break;
                        case PaymentWorkItem.WORK_TYPE_RESTART:
                            {
                                var user = new User();
                                user.Id = long.Parse(this.payment.Creator);
                                user.FirstName = "Unknown";
                                await TGBot.PushDialog(config.Accountant, new PaymentDetailDialog(this.payment.Creator, user, payment.Id), cancellationToken);
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
