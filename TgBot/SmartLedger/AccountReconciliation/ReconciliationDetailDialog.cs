using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

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

        public ReconciliationDetailDialog(ChatId chatId, User from, Guid reconciliationId) : base(chatId, from)
        {
            ReconciliationId = reconciliationId;
        }

        public override FormDialogField GetFieldDef(string key)
        {
            var service = new SmartLedgerService();
            var reconciliation = service.GetReconciliation(ReconciliationId);
            var choices = new List<FormFieldChoiceItem>();
            var w = reconciliation.WorkItemHead == null ? null : service.GetReconciliationWorkItem(reconciliation.WorkItemHead.Value);
            var e = service.GetEntity();
            if (e == null)
                throw new InvalidOperationException("Company not configured");
            if (w != null)
            {
                if (w.WorkType == ReconciliationWorkItem.WORK_TYPE_REQUEST 
                    && e.Owner==base.from.Id.ToString())
                {
                    choices.Add(new FormFieldChoiceItem(COMMAND_APPROVE, "Approve"));
                    choices.Add(new FormFieldChoiceItem(COMMAND_REJECT, "Reject"));
                }

                if (w.WorkType == ReconciliationWorkItem.WORK_TYPE_REJECTED
                    && reconciliation.Creator== base.from.Id.ToString()
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
                    await TGBot.PushDialog(this.from.Id.ToString(), new ReconciliationApproveDialog(this.chatId, this.from, this.ReconciliationId), cancellationToken);
                    break;
                case COMMAND_REJECT:
                    await TGBot.PushDialog(this.from.Id.ToString(), new ReconciliationRejectDialog(this.chatId, this.from, this.ReconciliationId), cancellationToken);
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
