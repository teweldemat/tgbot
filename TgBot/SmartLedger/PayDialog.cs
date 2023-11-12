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

    public class PayRejectDialog : RejectDialogBase
    {
        protected override int WorkType => PaymentWorkItem.WORK_TYPE_CANT_PAY;

        protected override IEnumerable<string> NextStage
        {
            get
            {
                var config = new SmartLedgerService().GetRuleData<SimplePaymentFlowConfiguration>();
                var ret = new List<String>();
                if (config == null)
                    return ret;
                
                if (config.Approver1 != null)
                    ret.Add(config.Approver1);
                return ret;
            }
        }

        public PayRejectDialog(ChatId chatId, User from, Guid paymentId) : base(chatId, from, paymentId)
        {
            this.PaymentId = paymentId;
        }
        protected override string GroupNotification(string rejecterName, Payment payment) =>null;

        protected override string OwnerNotification(string rejecterName, Payment payment) => null;
        


    }
    public class PayDialog : FormDialog
    {
        const String FIELD_NOTE = "Note";
        const string FIELD_ATTACHMENT_PREFIX = "Attachment";
        const string MORE_ATTACHMENT_PREFIX = "More Attachment";

        public Payment payment { get; set; }
        public override string FirstField => FIELD_ATTACHMENT_PREFIX+"0";

        public PayDialog(ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            var service = new SmartLedgerService();
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
                        Prompt = "Enter remarks",
                        FieldType = FieldType.Text,
                        NextField = null,
                    };
            }
            if (key.StartsWith(FIELD_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_ATTACHMENT_PREFIX.Length));
                String inst;
                if(index==0)
                {
                    var service = new SmartLedgerService();
                    var config = new SmartLedgerService().GetRuleData<SimplePaymentFlowConfiguration>();

                    var sources = service.GetPaymentSources(this.payment.Id);
                    sources = sources.Where(x => config.IsPayer(from.Id.ToString(), x.CashAccountId, payment.IsDeposit)).ToList();
                    if(this.payment.IsDeposit)
                        inst = $"Recieve from {this.payment.ToPayTo} as follows:";
                    else if(this.payment.IsTransferTransaction)
                        inst = $"Transfer to {service.GetCashAccount(this.payment.TransferTo.Value).Name} as follows:";
                    else
                        inst = $"Pay for {this.payment.ToPayTo} as follows:";
                    foreach (var s in sources)
                    {
                        inst += "\n"+Program.lm.payment_action(payment, service.GetCashAccount(s.CashAccountId).Name
                                                            , payment.TransferTo == null ? null : service.GetCashAccount(payment.TransferTo.Value).Name,instruction:true);
                    }
                    inst += "\nUpload attachments (e.g. deposit slip, receipt)";
                }
                else
                    inst = "\nUpload attachments (e.g. deposit slip, receipt)";
                return new FormDialogField
                {
                    Prompt = inst,
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
                        var ret = Task.FromResult("YES".Equals(d[key].Val()) ? FIELD_ATTACHMENT_PREFIX + (index + 1) : FIELD_NOTE);
                        return ret;
                    }
                };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();
            var config = new SmartLedgerService().GetRuleData<SimplePaymentFlowConfiguration>();

            var sources = service.GetPaymentSources(this.payment.Id);
            sources=sources.Where(x=>config.IsPayer(from.Id.ToString(),x.CashAccountId, payment.IsDeposit)).ToList();
            var total = sources.Sum(x => x.Amount);
            bool fullPayment = total == payment.Amount;

            service.AddPaymentWorkItem(
                userId:from.Id.ToString(),
                work:new PaymentWorkItem
                {
                    PaymentId=this.payment.Id,
                    WorkType = PaymentWorkItem.WORK_TYPE_PAY,
                    Note=(String)FieldData[FIELD_NOTE].Val(),
                }, 
                attachments: this.Pictures(FIELD_ATTACHMENT_PREFIX).Select(x => new WorkItemPicture
                {
                    Image = x.Image,
                    ImgeMime = x.ImageMime,
                    LinkedImage = x.ContentLink,
                    LinkedImageType = "1"
                }).ToList(), 
                completePayment:sources
                );
            try
            {
                await bot.SendTextMessageAsync(chatId, "Thank you for completing the payment.");
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error send confirmation for approval", ex);
            }
            try
            {
                var tgService = new TgBot.TgDb.TgDbService();
                var state = tgService.GetUserState(from.Id.ToString());
                var prof = service.GetUserProfile(from.Id.ToString());
                
                //notify group
                await SmartLedgerBot.NotifyGroups(bot, $"{prof.FullName} made {(fullPayment?"full payment": "partiall payment")} for the request {SmartLedgerBot.PaymentLink(payment.Id,payment.Reference)}", true, cancellationToken);

                //notify owner
                await bot.SendTextMessageAsync(chatId: payment.Creator,
                        text: $"{prof.FullName} made {(fullPayment ? "full payment" : "partiall payment")} for the request /{payment.Reference}",
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                
                //notify next stage
                if (config != null) 
                {
                    if (fullPayment)
                    {
                        var next = config.Accountant;
                        var user = new User();
                        user.Id = long.Parse(next);
                        user.FirstName = "Unknown";
                        await TGBot.PushDialog(next, new PaymentDetailDialog(next, user, payment.Id), cancellationToken);
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
