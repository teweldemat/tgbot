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

    public class AccountDialog : FormDialog
    {
        const string FIELD_NOTE = "Note";
        const string FIELD_ATTACHMENT_PREFIX = "Attachment";
        const string MORE_ATTACHMENT_PREFIX = "More Attachment";

        //persistent state
        public Guid PaymentId { get; set; }

        //transient
        TgDbService tgService;
        SmartLedgerService service;
        Payment payment { get; set; }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
            init();
        }
        void init()
        {
            payment = service.GetPayment(this.PaymentId);
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
            if (config == null)
                throw new UserFriendlyError("Configuration not set");


        }
        public AccountDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(chatId, from)
        {
            this.tgService = tgService;
            this.service = service;
            this.PaymentId = paymentId;
            if (this.service != null)
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
            using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
            {
                var service = serviceProvider.GetService<SmartLedgerService>();
                var sources = service.GetPaymentSources(payment.Id);

                service.AddPaymentWorkItem(from.Id.ToString(),
                    new PaymentWorkItem
                    {
                        PaymentId = payment.Id,
                        WorkType = PaymentWorkItem.WORK_TYPE_ACCOUNT,
                        Note = (string)FieldData[FIELD_NOTE].Val(),
                    },
                    Pictures(FIELD_ATTACHMENT_PREFIX).Select(x => new WorkItemPicture
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
                    var state = tgService.GetUserState(from.Id.ToString());
                    var prof = service.GetUserProfile(from.Id.ToString());

                    //notify group
                    await SmartLedgerBot.NotifyGroups(bot, tgService, $"{prof.FullName} completed the accounting for the request "
                        + $"{SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}", true, cancellationToken);

                    //notify owner
                    await bot.SendTextMessageAsync(chatId: payment.Creator,
                            text: $"{prof.FullName} completed the accounting for the request"
                                    + $"/{payment.Reference}",
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
}
