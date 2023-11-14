using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    public class ApproveRejectDialog : RejectDialogBase
    {
        public bool ReversePayment { get; set; }

        protected override IEnumerable<string> NextStage => null;
        public ApproveRejectDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId, bool reversePayment = false) 
            : base(service, tgService, chatId, from, paymentId)
        {
            ReversePayment = reversePayment;
        }


        [JsonIgnore]
        protected override int WorkType => PaymentWorkItem.WORK_TYPE_CANCELED;

        protected override string GroupNotification(string rejecterName, Payment payment)
        {
            return $"{rejecterName} rejected request "
                    + $"{SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}";
        }

        protected override string OwnerNotification(string rejecterName, Payment payment)
        {
            return $"{rejecterName} rejected your request "
                                + $"/{payment.Reference}";
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {

            service.AddPaymentWorkItem(from.Id.ToString(),
                new PaymentWorkItem
                {
                    PaymentId = PaymentId,
                    WorkType = WorkType,
                    Note = (string)FieldData[FIELD_NOTE].Val()
                }, reversePayment: ReversePayment);
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
}
