using System.Threading.Tasks;
using System.Threading;
using System;
using Telegram.Bot.Types;
using Telegram.Bot;
using TgBot.TgDb;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace TgBot.SmartLedger.Dialog
{
    public class ReopenPaymentDialog : FormDialog
    {
        const string FIELD_NOTE = "Note";

        // Persistent
        public Guid PaymentId { get; set; }

        // Transient
        SmartLedgerService service;
        TgDbService tgService;
        Payment payment { get; set; }

        void init()
        {
            payment = service.GetPayment(PaymentId);
            // Additional initialization as needed
        }

        public ReopenPaymentDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            this.service = service;
            this.tgService = tgService;
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
                        Prompt = "Please enter a remark for reopening the payment",
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
                    WorkType = PaymentWorkItem.WORK_TYPE_REOPEN,
                    Note = (string)FieldData[FIELD_NOTE].Val(),
                });

            string message = "The payment has been reopened.";
            try
            {
                await bot.SendTextMessageAsync(chatId, message);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error sending confirmation for reopening", ex);
            }

            try
            {
                // Notify relevant users or groups about the reopening
                string messageGroup = $"{prof.FullName} reopened request {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
                await SmartLedgerBot.NotifyGroups(bot, tgService, messageGroup, true, cancellationToken);

                string messageOwner = $"{prof.FullName} reopened your request /{payment.Reference}";
                await bot.SendTextMessageAsync(
                    chatId: payment.Creator,
                    text: messageOwner,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken
                );

                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                var next = config.Accountant;
                var user = new User();
                user.Id = long.Parse(next);
                user.FirstName = "Unknown";
                await TGBot.PushDialog(next, new PaymentDetailDialog(service, tgService, next, user, payment.Id), cancellationToken);

            }
            catch (Exception ex)
            {
                TGBot.LogException("Error notifying groups and relevant users", ex);
            }

            return DialogResult.Terminated;
        }

    }

}
