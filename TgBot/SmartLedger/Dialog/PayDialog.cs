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
    public class PayDialog : FormDialog
    {
        const string FIELD_NOTE = "Note";
        const string FIELD_ATTACHMENT_PREFIX = "Attachment";
        const string MORE_ATTACHMENT_PREFIX = "More Attachment";
        const string FIELD_PAYER_FEE = "PayerFee_";
        const string FIELD_PAYEE_FEE = "PayeeFee_";

        const double MAX_FEE_PROPORTION = 0.1;
        public Guid PaymentId { get; set; } 
        Payment payment { get; set; }
        SmartLedgerService service;
        TgDbService tgService;
        void init()
        {
            payment = service.GetPayment(this.PaymentId);
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new UserFriendlyError("Configuration not set");

        }
        public PayDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            this.tgService = tgService;
            this.service = service;
            this.PaymentId= paymentId;
            if (this.service != null)
                init();
        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetServiceAssert<SmartLedgerService>();
            tgService = services.GetServiceAssert<TgDbService>();
            init();
        }

        public override string FirstField => FIELD_PAYER_FEE + "0";
        public override FormDialogField GetFieldDef(string key)
        {
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();

            var allSources = service.GetPaymentSources(payment.Id);
            var sources = allSources.Where(x => config.IsPayer(from.Id.ToString(), x.CashAccountId, payment.IsDeposit))
                .OrderBy(x => x.CashAccountId)
                .ToList();

            if (key.StartsWith(FIELD_PAYER_FEE) || key.StartsWith(FIELD_PAYEE_FEE))
            {
                int sourceIndex = int.Parse(key.Split('_')[1]);
                var source = sources.ElementAtOrDefault(sourceIndex);
                if (source == null) return null; // Error handling if source doesn't exist
                var account = service.GetCashAccount(source.CashAccountId);
                bool isPayerFee = key.StartsWith("PayerFee_");
                string prompt = isPayerFee ? $"Enter the transaction fee paid by the payer for {account.Name}" :
                                             $"Enter the transaction fee paid by the beneficiary for {account.Name}";

                string nextKey = isPayerFee ? $"{FIELD_PAYEE_FEE}{sourceIndex}" : sourceIndex + 1 < sources.Count ? $"{FIELD_PAYER_FEE}{sourceIndex + 1}" : FIELD_ATTACHMENT_PREFIX + "0";

                return new FormDialogField
                {
                    Prompt = prompt,
                    FieldType = FieldType.Text,
                    NextField = d => Task.FromResult(nextKey),
                    ParseFunction = (bot, t, c) =>
                    {
                        if (double.TryParse(t, out var d) && d >= 0)
                        {
                            var feeAmount = IntData.toIntMoney(d);
                            if (feeAmount <= MAX_FEE_PROPORTION * source.Amount)
                            {
                                return Task.FromResult(new ParseResult { Data = feeAmount });
                            }
                            else
                            {
                                return Task.FromResult(new ParseResult { Error = "Fee cannot exceed 10% of the payment amount" });
                            }
                        }
                        else
                        {
                            return Task.FromResult(new ParseResult { Error = "Invalid fee amount" });
                        }
                    }
                };
            }
            if (key.StartsWith(FIELD_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_ATTACHMENT_PREFIX.Length));
                string inst;
                if (index == 0)
                {
                    if (payment.IsDeposit)
                        inst = $"Recieve from {payment.ToPayTo} as follows:";
                    else if (payment.IsTransferTransaction)
                        inst = $"Transfer to {service.GetCashAccount(payment.TransferTo.Value).Name} as follows:";
                    else
                        inst = $"Pay for {payment.ToPayTo} as follows:";
                    foreach (var s in sources)
                    {
                        inst += "\n" + Program.lm.payment_action(payment, service.GetCashAccount(s.CashAccountId).Name
                                                            , payment.TransferTo == null ? null : service.GetCashAccount(payment.TransferTo.Value).Name, instruction: true);
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

            throw new InvalidOperationException($"Invalid field {key}");
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();

            var allSources = service.GetPaymentSources(payment.Id);
            var sources = allSources.Where(x => config.IsPayer(from.Id.ToString(), x.CashAccountId, payment.IsDeposit))
                .OrderBy(x => x.CashAccountId)
                .ToList();

            var total = sources.Sum(x => x.Amount);
            bool fullPayment = total == payment.Amount;

            foreach (var source in sources)
            {
                int sourceIndex = sources.IndexOf(source);
                source.PayerFee = (long)FieldData[$"PayerFee_{sourceIndex}"].Val();
                source.PayeeFee = (long)FieldData[$"PayeeFee_{sourceIndex}"].Val();
            }


            service.AddPaymentWorkItem(
                userId: from.Id.ToString(),
                work: new PaymentWorkItem
                {
                    PaymentId = payment.Id,
                    WorkType = PaymentWorkItem.WORK_TYPE_PAY,
                    Note = (string)FieldData[FIELD_NOTE].Val(),
                },
                attachments: Pictures(FIELD_ATTACHMENT_PREFIX).Select(x => new WorkItemPicture
                {
                    Image = x.Image,
                    ImgeMime = x.ImageMime,
                    LinkedImage = x.ContentLink,
                    LinkedImageType = "1"
                }).ToList(),
                completePayment: sources
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
                var state = tgService.GetUserState(from.Id.ToString());
                var prof = service.GetUserProfile(from.Id.ToString());

                //notify group
                await SmartLedgerBot.NotifyGroups(bot, tgService, $"{prof.FullName} made {(fullPayment ? "full payment" : "partiall payment")} for the request {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}", true, cancellationToken);

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
                        await TGBot.PushDialog(next, new PaymentDetailDialog(service, tgService, next, user, payment.Id), cancellationToken);
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
