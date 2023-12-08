using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    public class CheckRejectDialog : RejectDialogBase
    {
        protected override int WorkType => PaymentWorkItem.WORK_TYPE_CANCELED;

        protected override IEnumerable<string> NextStage => null;

        public CheckRejectDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(service, tgService, chatId, from, paymentId)
        {
            PaymentId = paymentId;
        }
        protected override string GroupNotification(string rejecterName, Payment payment)
        {
            return $"{rejecterName} checked and rejeced request {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
        }
        protected override string OwnerNotification(string rejecterName, Payment payment)
        {
            return $"{rejecterName} checked and rejected your request /{payment.Reference}";
        }
    }
    public abstract class RejectDialogBase : FormDialog
    {
        protected const string FIELD_NOTE = "Note";
        protected const string FIELD_CONFIRM = "Confirm";
        public override string FirstField => FIELD_CONFIRM;
        public Guid PaymentId { get; set; }

        protected SmartLedgerService service;
        protected TgDbService tgService;
        public RejectDialogBase(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            PaymentId = paymentId;
            this.service = service;
            this.tgService = tgService;
        }

        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
        }
        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = "Why are you rejecting this request?",
                        FieldType = FieldType.Text,
                        NextField = null,
                    };
                case FIELD_CONFIRM:
                    return new FormDialogField
                    {
                        Prompt = "Are you sure you want to reject this request?",
                        FieldType = FieldType.Choices,
                        Choices = new[] { new FormFieldChoiceItem("YES"), new("NO") },
                        NextField = d => Task.FromResult(d[FIELD_CONFIRM].Val<string>().Equals("YES") ? FIELD_NOTE : null),
                    };
            }
            return null;
        }
        protected abstract int WorkType { get; }
        protected abstract IEnumerable<string> NextStage { get; }
        protected abstract string GroupNotification(string rejecterName, Payment payment);
        protected abstract string OwnerNotification(string rejecterName, Payment payment);
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {

            service.AddPaymentWorkItem(from.Id.ToString(),
                new PaymentWorkItem
                {
                    PaymentId = PaymentId,
                    WorkType = WorkType,
                    Note = (string)FieldData[FIELD_NOTE].Val()
                });
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
                var state = tgService.GetUserState(from.Id.ToString());
                var prof = service.GetUserProfile(from.Id.ToString());
                var payment = service.GetPayment(PaymentId);

                //notify group
                var groupNotif = GroupNotification(prof.FullName, payment);
                if (groupNotif != null)
                    await SmartLedgerBot.NotifyGroups(bot, tgService, groupNotif, true, cancellationToken);

                //notify owner
                var ownerNotification = OwnerNotification(prof.FullName, payment);
                if (ownerNotification != null)
                    await bot.SendTextMessageAsync(chatId: payment.Creator,
                            text: ownerNotification,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);

                //notify next stage
                var next = NextStage;
                if (next != null)
                {
                    foreach (var v in next)
                    {
                        var user = new User();
                        user.Id = long.Parse(v);
                        user.FirstName = "Unknown";
                        await TGBot.PushDialog(v, new PaymentDetailDialog(service, tgService, user.Id, user, PaymentId), cancellationToken);
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
    public class CheckAcceptDialog : FormDialog
    {
        const string FIELD_NOTE = "Note";
        const string FIELD_SOURCE_PREFIX = "Source";
        const string FIELD_AMOUNT_PREFIX = "Amount";
        const string FIELD_PAYMENT_INST = "AccountNumber";
        public List<Guid> CashAccounts { get; set; }
        public Guid PaymentId { get; set; }
        Payment payment { get; set; }
        public override string FirstField => FIELD_NOTE;
        SmartLedgerService service;
        TgDbService tgService;
        void init()
        {
            payment = service.GetPayment(PaymentId);
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new Exception("Configuration not set");
            CashAccounts = service.GetCashAccounts().Select(x => x.Id).ToList();
            if (CashAccounts.Count == 0)
                throw new UserFriendlyError("Payment can't be checked because no cash account is set");

        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetServiceAssert<SmartLedgerService>();
            tgService = services.GetServiceAssert<TgDbService>();
            init();
        }
        public CheckAcceptDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            this.tgService = tgService;
            this.service = service;
            PaymentId = paymentId;
            if (this.service != null)
                init();
        }

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = "Please enter some note",
                        FieldType = FieldType.Text,
                        NextField = d =>
                        {
                            if (CashAccounts.Count == 0)
                                throw new Exception("Payment can't be accepted becuase account is not registered");
                            if (CashAccounts.Count == 1)
                                return Task.FromResult<string>(null);
                            return Task.FromResult(FIELD_SOURCE_PREFIX + "0");
                        },
                    };
            }
            if (key.StartsWith(FIELD_SOURCE_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_SOURCE_PREFIX.Length));
                var ca = service.GetCashAccount(CashAccounts[index]);
                var accounts = service.GetCashAccounts();
                var used = FieldData.Where(kv => kv.Key.StartsWith(FIELD_SOURCE_PREFIX)).Select(kv => (Guid)kv.Value.Val()).ToList();
                if (payment.IsTransferTransaction)
                    used.Add(payment.TransferTo.Value);
                return new FormDialogField
                {
                    Prompt = payment.IsDeposit ? $"Enter the destination account to deposit to" : $"Enter the source to pay from",
                    Choices = accounts.Where(x => !used.Contains(x.Id)).Select(x =>
                        new FormFieldChoiceItem(x.Id.ToString(), x.Name)).ToList(),
                    FieldType = FieldType.Choices,
                    NextField = d => Task.FromResult(FIELD_AMOUNT_PREFIX + index),
                    ParseFunction = (b, t, c) =>
                    {
                        return Task.FromResult(new ParseResult { Data = Guid.Parse(t) });
                    }
                };
            }
            if (key.StartsWith(FIELD_AMOUNT_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_AMOUNT_PREFIX.Length));
                var cashAccount = (Guid)FieldData[FIELD_SOURCE_PREFIX + index].Val();
                var ca = service.GetCashAccount(cashAccount);
                if (ca == null)
                    throw new UserFriendlyError("Cash account doesn't exist anymore");

                return new FormDialogField
                {
                    Prompt = payment.IsDeposit ? $"Enter the amount to add to {ca.Name}" : $"Enter the amount to pay from {ca.Name}",
                    FieldType = FieldType.Text,
                    NextField = index == CashAccounts.Count - 1
                    ? null : d =>
                    {

                        return Task.FromResult(FIELD_PAYMENT_INST + index);
                    },
                    ParseFunction = (b, t, c) =>
                    {
                        if (!double.TryParse(t, out var v) || v <= 0)
                            return Task.FromResult(new ParseResult { Error = "Please enter valid amount" });
                        var amount = IntData.toIntMoney(v);
                        long total = 0;
                        int count = 0;
                        foreach (var kv in FieldData)
                        {
                            if (kv.Key.StartsWith(FIELD_AMOUNT_PREFIX))
                            {
                                total += (long)kv.Value.Val();
                                count++;
                            }
                        }
                        total += amount;
                        count++;
                        if (total > payment.PositiveAmount)
                        {
                            return Task.FromResult(new ParseResult
                            {
                                Error = $"Total checked {IntData.toString(total)} exceeds the requested amount {IntData.toString(payment.PositiveAmount)}"
                            });
                        }
                        if (count == CashAccounts.Count && total != payment.Amount)
                        {
                            return Task.FromResult(new ParseResult
                            {
                                Error = $"Total checked {IntData.toString(total)} doesn't match the requested amount {IntData.toString(payment.PositiveAmount)}"
                            });
                        }
                        return Task.FromResult(new ParseResult { Data = amount });
                    }
                };
            }
            if (key.StartsWith(FIELD_PAYMENT_INST))
            {
                int index = int.Parse(key.Substring(FIELD_PAYMENT_INST.Length));
                var ca = service.GetCashAccount(CashAccounts[index]);
                var accounts = service.GetCashAccounts();
                var used = FieldData.Where(kv => kv.Key.StartsWith(FIELD_SOURCE_PREFIX)).Select(kv => (Guid)kv.Value.Val()).ToList();
                string prompt;
                if (payment.IsDeposit)
                    prompt = $"Enter deposit instructions";
                else if (payment.IsTransferTransaction)
                    prompt = $"Enter transfer instructions";
                else
                    prompt = $"Enter payment instructions";

                return new FormDialogField
                {
                    Prompt = prompt,
                    FieldType = FieldType.Text,
                    NextField = d =>
                    {
                        long total = d.Sum(kv => kv.Key.StartsWith(FIELD_AMOUNT_PREFIX) ? (long)kv.Value.Val() : 0);
                        if (total >= payment.Amount)
                            return Task.FromResult<string>(null);
                        return Task.FromResult(FIELD_SOURCE_PREFIX + (index + 1));
                    }
                };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var sources = new List<PaymentSource>();
            if (CashAccounts.Count == 1)
            {
                sources.Add(new PaymentSource
                {
                    CashAccountId = CashAccounts[0],
                    Amount = payment.Amount
                });
            }
            else
            {

                foreach (var kv in FieldData)
                {
                    if (kv.Key.StartsWith(FIELD_SOURCE_PREFIX))
                    {
                        int index = int.Parse(kv.Key.Substring(FIELD_SOURCE_PREFIX.Length));
                        sources.Add(new PaymentSource
                        {
                            CashAccountId = (Guid)kv.Value.Val(),
                            Amount = payment.IsDeposit ? -(long)FieldData[FIELD_AMOUNT_PREFIX + index].Val()
                            : (long)FieldData[FIELD_AMOUNT_PREFIX + index].Val(),
                            PaymentInstruction = (string)FieldData[FIELD_PAYMENT_INST + index].Val(),
                        }
                        );
                    }
                }
            }
            service.AddPaymentWorkItem(
                userId: from.Id.ToString(),
                work: new PaymentWorkItem
                {
                    PaymentId = payment.Id,
                    WorkType = PaymentWorkItem.WORK_TYPE_CHECK,
                    Data = Newtonsoft.Json.JsonConvert.SerializeObject(sources),
                    Note = (string)FieldData[FIELD_NOTE].Val()
                },
                setPaymentSources: sources
                );
            try
            {
                await bot.SendTextMessageAsync(chatId, "Thank you for checking the request.");
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error send confirmation for checking ", ex);
            }
            try
            {
                var state = tgService.GetUserState(from.Id.ToString());
                var prof = service.GetUserProfile(from.Id.ToString());

                //notify group
                await SmartLedgerBot.NotifyGroups(bot, tgService, $"{prof.FullName} checked and gave an ok to the request {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}", true, cancellationToken);

                //notify creator
                await bot.SendTextMessageAsync(chatId: payment.Creator,
                        text: $"{prof.FullName} checked and gave an ok to your the request /{payment.Reference}",
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);

                //notify next stage
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                if (config != null)
                {
                    var next = config.Approver1;
                    var user = new User();
                    user.Id = long.Parse(next);
                    user.FirstName = "Unknown";
                    await TGBot.PushDialog(next, new PaymentDetailDialog(service, tgService, next, user, payment.Id), cancellationToken);
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
