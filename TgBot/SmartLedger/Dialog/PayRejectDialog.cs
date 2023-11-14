using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    public class PayRejectDialog : RejectDialogBase
    {
        protected override IEnumerable<string> NextStage
        {
            get
            {
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                var ret = new List<string>();
                if (config == null)
                    return ret;

                if (config.Approver1 != null)
                    ret.Add(config.Approver1);
                return ret;
            }
        }
        public PayRejectDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, Guid paymentId) : base(service, tgService, chatId, from, paymentId)
        {
            PaymentId = paymentId;
        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
        }
        protected override int WorkType => PaymentWorkItem.WORK_TYPE_CANT_PAY;
        protected override string GroupNotification(string rejecterName, Payment payment) => null;
        protected override string OwnerNotification(string rejecterName, Payment payment) => null;
    }
}
