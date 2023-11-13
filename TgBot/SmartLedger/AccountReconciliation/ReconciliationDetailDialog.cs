using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.SmartLedger.AccountReconciliation
{
    public class ReconciliationDetailDialog : FormDialog
    {
        const string FIELD_COMMAND = "Command";
        const string COMMAND_APPROVE = "Approve";
        const string COMMAND_REJECT = "Reject";
        const string COMMAND_CANCEL = "Cancel";
        const string COMMAND_UPDATE = "UpdateReconciliation";
        public Guid ReconciliationId { get; set; }

        public override string FirstField => FIELD_COMMAND;
        SmartLedgerService service;
        public ReconciliationDetailDialog(SmartLedgerService service, ChatId chatId, User from, Guid reconciliationId) : base(chatId, from)
        {
            ReconciliationId = reconciliationId;
            this.service = service;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<SmartLedgerService>();
        }

        public override FormDialogField GetFieldDef(string key)
        {
            var reconciliation = service.GetReconciliation(ReconciliationId);
            var choices = new List<FormFieldChoiceItem>();
            var w = reconciliation.WorkItemHead == null ? null : service.GetReconciliationWorkItem(reconciliation.WorkItemHead.Value);
            var e = service.GetEntity();
            if (e == null)
                throw new InvalidOperationException("Company not configured");
            if (w != null)
            {
                if (w.WorkType == ReconciliationWorkItem.WORK_TYPE_REQUEST
                    && e.Owner == base.from.Id.ToString())
                {
                    choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE, "Approve"));
                    choices.Add(new FormFieldChoiceItem(COMMAND_REJECT, "Reject"));
                }

                if (w.WorkType == ReconciliationWorkItem.WORK_TYPE_REJECTED
                    && reconciliation.Creator == base.from.Id.ToString()
                    )
                {
                    choices.Add(new FormFieldChoiceItem(COMMAND_CANCEL, "Cancel"));
                    choices.Add(new FormFieldChoiceItem(COMMAND_UPDATE, "Update Reconciliation"));
                }

            }

            return new FormDialogField
            {
                FieldType = FieldType.Choices,
                Choices = choices,
                PromptHtml = SmartLedgerBot.FormatReconciliationDetailHtml(reconciliation.Id)
            };
        }

        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var command = FieldData[FIELD_COMMAND].Val<string>();

            switch (command)
            {
                case COMMAND_APPROVE:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ReconciliationApproveDialog(service, this.chatId, this.from, this.ReconciliationId), cancellationToken);
                    break;
                case COMMAND_REJECT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ReconciliationRejectDialog(service, this.chatId, this.from, this.ReconciliationId), cancellationToken);
                    break;
                case COMMAND_CANCEL:
                    // Logic for cancel action
                    break;
                case COMMAND_UPDATE:
                    // Logic for update reconciliation action
                    break;
                    // Add more cases as necessary
            }

            return DialogResult.Terminated;
        }
    }
}
