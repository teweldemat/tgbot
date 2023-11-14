using System.Threading.Tasks;
using System.Threading;
using System;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using TgBot.TgDb;
using Microsoft.Extensions.DependencyInjection;

namespace TgBot.SmartLedger.Dialog
{
    public class VoidPaymentDialog : FormDialog
    {
        const string FIELD_NOTE = "Note";

        // Persistent
        public Guid PaymentId { get; private set; }

        // Transient
        private SmartLedgerService service;
        private TgDbService tgService;
        private Payment payment;

        private void Init()
        {
            payment = service.GetPayment(PaymentId);
            // Additional initialization if necessary
        }

        public VoidPaymentDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId)
            : base(chatId, from)
        {
            this.service = service;
            this.tgService = tgService;
            PaymentId = paymentId;
            if (this.service != null)
                Init();
        }

        public override void SetServices(IServiceProvider services)
        {
            service = services.GetServiceAssert<SmartLedgerService>();
            tgService = services.GetServiceAssert<TgDbService>();
            Init();
        }

        public override string FirstField => FIELD_NOTE;

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = "Please enter a reason for voiding the payment",
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

            // Logic to void the payment
            service.AddPaymentWorkItem(
                this.from.Id.ToString(),
                new PaymentWorkItem
                {
                    PaymentId= PaymentId,
                    Data="Void",
                    WorkType=PaymentWorkItem.WORK_TYPE_VOID
                },
                reversePayment:true
                );

            // Construct notification messages
            var message = "The payment has been voided.";
            var messageGroup = $"{prof.FullName} voided the payment {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
            var messageOwner = $"{prof.FullName} voided your payment /{payment.Reference}";

            // Send notifications
            try
            {
                await bot.SendTextMessageAsync(chatId, message);

                // Notify group
                if (messageGroup != null)
                    await SmartLedgerBot.NotifyGroups(bot, tgService, messageGroup, true, cancellationToken);

                // Notify payment creator
                if (messageOwner != null)
                    await bot.SendTextMessageAsync(chatId: payment.Creator, text: messageOwner, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error while processing void payment", ex);
            }

            return DialogResult.Terminated;
        }
    }

}
