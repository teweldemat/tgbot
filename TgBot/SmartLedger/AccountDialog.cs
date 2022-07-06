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

    public class AccountDialog: FormDialog
    {
        const String FIELD_NOTE = "Note";
        const string FIELD_ATTACHMENT_PREFIX = "Attachment";
        const string MORE_ATTACHMENT_PREFIX = "More Attachment";

        public Payment payment { get; set; }
        public override string FirstField => FIELD_NOTE;

        public AccountDialog(ChatId chatId, User from, Guid paymentId) : base(chatId, from)
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
                        NextField = d => Task.FromResult(FIELD_ATTACHMENT_PREFIX + "0")
                    };
            }
            if (key.StartsWith(FIELD_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_ATTACHMENT_PREFIX.Length));
                return new FormDialogField
                {
                    Prompt = "Upload attachments (e.g. journal entires, vouchers, etc)",
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
                        var ret = Task.FromResult("YES".Equals(d[key].Val()) ? FIELD_ATTACHMENT_PREFIX + (index + 1) : null);
                        return ret;
                    }
                };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();
            var sources = service.GetPaymentSources(this.payment.Id);

            service.AddWorkItem(from.Id.ToString(),
                new PaymentWorkItem
                {
                    PaymentId=this.payment.Id,
                    WorkType = PaymentWorkItem.WORK_TYPE_ACCOUNT,
                    Note=(String)FieldData[FIELD_NOTE].Val(),
                }, 
                this.Pictures(FIELD_ATTACHMENT_PREFIX).Select(x => new WorkItemPicture
                {
                    Image = x.Image,
                    ImgeMime = x.ImageMime,
                    LinkedImage = x.ContentLink,
                    LinkedImageType = "1"
                }).ToList());
            try
            {
                await bot.SendTextMessageAsync(chatId, "Thank you for doing the accounting.");
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error sending confirmation for accounting", ex);
            }
            try
            {
                var tgService = new TgBot.TgDb.TgDbService();
                var state = tgService.GetUserState(from.Id.ToString());
                var prof = service.GetUserProfile(from.Id.ToString());
                
                //notify group
                await SmartLedgerBot.NotifyGroups(bot, $"{prof.FullName} completed the accounting for the request "
                    + $"{SmartLedgerBot.PaymentLink(payment.Id,payment.Reference)}", true, cancellationToken);

                //notify owner
                await bot.SendTextMessageAsync(chatId: payment.Creator,
                        text: $"{prof.FullName} completed the accounting for the request"
                                + $"/{payment.Reference}",
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                
                //notify next stage
                var config = new SmartLedgerService().GetRuleData<SimplePaymentFlowConfiguration>();
                if (config != null)
                {
                    var next = config.Approver1;
                    var user = new User();
                    user.Id = long.Parse(next);
                    user.FirstName = "Unknown";
                    await TGBot.PushDialog(next, new PaymentDetailDialog(next, user, payment.Id), cancellationToken);
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
