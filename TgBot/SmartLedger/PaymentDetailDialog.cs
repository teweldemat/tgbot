using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.SmartLedger
{
    public class PaymentDetailDialog : FormDialog
    {
        const String FIELD_COMAND = "Command";
        const String COMMAND_CHECK = "Checked";
        const String COMMAND_CHECK_REJECT = "CheckedReject";
        const String COMMAND_APPROVE = "Approve";
        const String COMMAND_APPROVE_REJECT = "ApprovalReject";
        const String COMMAND_APPROVE_REJECT_REVERSE_PAYMENT = "ApprovalRejectReversePayment";
        const String COMMAND_PAY = "Pay";
        const String COMMAND_CANT_PAY = "CantPay";
        const String COMMAND_ACCOUNT = "Account";
        const String COMMAND_CLOSE = "Close";
        const String COMMAND_SEND_BACK_TO_PAYMENT = "SendBankToPayment";
        const String COMMAND_SEND_BANK_TO_ACCOUNTING = "SendBackToAccount";
        const string COMMAND_RESTART = "Restart";
        const string COMMAND_UPDATE_REQUEST = "UpdateRequest";
        public Guid PaymentId { get; set; }

        public override string FirstField => FIELD_COMAND;
        SmartLedgerService service;
        TgDbService tgService;
        public PaymentDetailDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            this.PaymentId = paymentId;
            this.service = service;
            this.tgService = tgService;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<SmartLedgerService>();
            this.tgService = services.GetService<TgDbService>();
        }
        public override FormDialogField GetFieldDef(string key)
        {

            var payment = service.GetPayment(PaymentId);
            var choices = new List<FormFieldChoiceItem>();
            var w = payment.WorkItemHead == null ? null : service.GetWorkItem(payment.WorkItemHead.Value);
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<SimplePaymentFlowConfiguration>(service.GetRule().Rule);
            string PaymentInst = "";

            if (w != null)
            {
                //checking
                if (w.WorkType == PaymentWorkItem.WORK_TYPE_RESTART
                    && base.from.Id.ToString().Equals(payment.Creator))
                {
                    choices.Add(new FormFieldChoiceItem(COMMAND_UPDATE_REQUEST, "Update Request"));
                }
                //checking
                if (w.WorkType == PaymentWorkItem.WORK_TYPE_CREATE
                    && base.from.Id.ToString().Equals(config.Checker1))
                {
                    choices.Add(new FormFieldChoiceItem(COMMAND_CHECK, "Checked"));
                    choices.Add(new FormFieldChoiceItem(COMMAND_CHECK_REJECT, "Reject"));
                }


                var sources = service.GetPaymentSources(payment.Id);

                //first approval
                if (
                    (w.WorkType == PaymentWorkItem.WORK_TYPE_CHECK
                    && base.from.Id.ToString().Equals(config.Approver1)
                    )
                    ||
                    (w.WorkType == PaymentWorkItem.WORK_TYPE_CANT_PAY
                    && config.IsApprover(from.Id.ToString())
                    )
                    ||
                    (w.WorkType == PaymentWorkItem.WORK_TYPE_ACCOUNT
                    && config.IsApprover(from.Id.ToString())
                    )
                    )
                {
                    if (w.WorkType == PaymentWorkItem.WORK_TYPE_ACCOUNT)
                    {
                        choices.Add(new FormFieldChoiceItem(COMMAND_CLOSE, "Close request"));
                        choices.Add(new FormFieldChoiceItem(COMMAND_SEND_BANK_TO_ACCOUNTING, "Send Back to Accounting"));
                    }
                    else if (w.WorkType == PaymentWorkItem.WORK_TYPE_CANT_PAY)
                    {
                        var paid = new HashSet<String>();
                        service.ForEachWorkItem(this.PaymentId, x =>
                        {
                            if (x.WorkType == PaymentWorkItem.WORK_TYPE_PAY)
                            {
                                if (!paid.Contains(x.UserId))
                                    paid.Add(x.UserId);
                            }
                            return true;
                        });
                        long paidAmount = 0;
                        if (paid.Count > 0)
                        {
                            sources.ForEach(x =>
                            {
                                if (paid.Contains(config.GetPayer(x.CashAccountId, payment.IsDeposit)))
                                {
                                    paidAmount += x.Amount;
                                }
                            });
                        }
                        if (paidAmount > 0)
                        {
                            PaymentInst = $"{IntData.toString(paidAmount)} Birr is paid";
                            choices.Add(new FormFieldChoiceItem(COMMAND_SEND_BACK_TO_PAYMENT, "Send Back to Payment"));
                            choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE_REJECT_REVERSE_PAYMENT, "Close (Cancel Partial Payment)"));
                            choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE_REJECT, "Close (Retain Partial Payment)"));
                        }
                        else
                        {
                            choices.Add(new FormFieldChoiceItem(COMMAND_SEND_BACK_TO_PAYMENT, "Send Back to Payment"));
                            choices.Add(new FormFieldChoiceItem(COMMAND_RESTART, "Restart"));
                            choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE_REJECT, "Close"));
                        }
                    }
                    else
                    {
                        choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE, "Approve"));
                        choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE_REJECT, "Reject"));
                    }
                }


                long totalPaid = 0;
                if (w.WorkType == PaymentWorkItem.WORK_TYPE_PAY)
                {
                    var sourcesByGuid = new Dictionary<Guid, PaymentSource>(sources.Select(x => new KeyValuePair<Guid, PaymentSource>(x.CashAccountId, x)));
                    service.ForEachWorkItem(payment.Id, x =>
                    {
                        if (x.WorkType == PaymentWorkItem.WORK_TYPE_PAY)
                        {
                            foreach (var acid in config.GetAccountsOfPayer(x.UserId, payment.IsDeposit))
                                if (sourcesByGuid.ContainsKey(acid))
                                    totalPaid += sourcesByGuid[acid].Amount;
                        }
                        return true;
                    });
                }
                bool paymentStage =
                    w.WorkType == PaymentWorkItem.WORK_TYPE_APPROVE
                    || w.WorkType == PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT
                    || (w.WorkType == PaymentWorkItem.WORK_TYPE_PAY && totalPaid < payment.Amount);

                //payment
                if (paymentStage && config.IsPayer(base.from.Id.ToString(), sources, payment.IsDeposit)
                    )
                {
                    foreach (var s in sources)
                    {

                        if (config.IsPayer(from.Id.ToString(), s.CashAccountId, payment.IsDeposit))
                        {
                            String str;
                            if (payment.IsDeposit)
                                str = $"Deposit {IntData.toString(-s.Amount)} Birr to {service.GetCashAccount(s.CashAccountId).Name}";
                            else
                                str = $"Pay {IntData.toString(s.Amount)} Birr from {service.GetCashAccount(s.CashAccountId).Name}";
                            PaymentInst += "<pre>\n</pre>" + str;
                            if (!String.IsNullOrEmpty(s.PaymentInstruction))
                                PaymentInst += $"<pre>\n</pre> {s.PaymentInstruction}";
                        }
                    }

                    choices.Add(new FormFieldChoiceItem(COMMAND_PAY, payment.IsDeposit ? "Received" : "Paid"));
                    choices.Add(new FormFieldChoiceItem(COMMAND_CANT_PAY, payment.IsDeposit ? "Can't Receive" : "Can't Pay"));
                }


                //accounting
                if ((w.WorkType == PaymentWorkItem.WORK_TYPE_PAY
                    && !paymentStage
                    && from.Id.ToString().Equals(config.Accountant))
                    || w.WorkType == PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING
                    )
                {
                    choices.Add(new FormFieldChoiceItem("Account", COMMAND_ACCOUNT));
                }

            }
            return new FormDialogField
            {
                FieldType = FieldType.Choices,
                Choices = choices,
                PromptHtml = SmartLedgerBot.FormatPaymentDetailHtml(payment.Id) + PaymentInst
            };
        }

        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var command = this.FieldData[FIELD_COMAND].Val<String>();

            switch (command)
            {
                case COMMAND_CHECK:
                    await TGBot.PushDialog(this.from.Id.ToString(), new CheckAcceptDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_CHECK_REJECT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new CheckRejectDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_APPROVE_REJECT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveRejectDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_APPROVE_REJECT_REVERSE_PAYMENT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveRejectDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_PAY:
                    await TGBot.PushDialog(this.from.Id.ToString(), new PayDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_CANT_PAY:
                    await TGBot.PushDialog(this.from.Id.ToString(), new PayRejectDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_ACCOUNT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new AccountDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_APPROVE:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveAcceptDialog(service, tgService, this.chatId, this.from, this.PaymentId), cancellationToken);
                    break;
                case COMMAND_SEND_BACK_TO_PAYMENT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveAcceptDialog(service, tgService, this.chatId, this.from, this.PaymentId, approveType: PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT), cancellationToken);
                    break;
                case COMMAND_SEND_BANK_TO_ACCOUNTING:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveAcceptDialog(service, tgService, this.chatId, this.from, this.PaymentId, approveType: PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING), cancellationToken);
                    break;
                case COMMAND_CLOSE:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveAcceptDialog(service, tgService, this.chatId, this.from, this.PaymentId, approveType: PaymentWorkItem.WORK_TYPE_CLOSE), cancellationToken);
                    break;
                case COMMAND_RESTART:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ApproveAcceptDialog(service, tgService, this.chatId, this.from, this.PaymentId, approveType: PaymentWorkItem.WORK_TYPE_RESTART), cancellationToken);
                    break;
                case COMMAND_UPDATE_REQUEST:
                    var payment = service.GetPayment(this.PaymentId);
                    await TGBot.PushDialog(this.from.Id.ToString(), new RequestPaymentDialog(service,tgService, this.chatId, this.from, paymentType: payment.PaymentType, restartPayment: this.PaymentId), cancellationToken);
                    break;

            }
            return DialogResult.Terminated;
        }
    }
}
