using System.Threading.Tasks;
using System.Threading;
using System;
using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.CodeAnalysis;

namespace TgBot.SmartLedger.AccountReconciliation
{
    public class ReconciliationApproveDialog : FormDialog
    {
        const String FIELD_NOTE = "Note";
        public Guid ReconciliationId { get; set; }
        public override string FirstField => FIELD_NOTE;

        public ReconciliationApproveDialog(ChatId chatId, User from, Guid reconciliationId) : base(chatId, from)
        {
            this.ReconciliationId = reconciliationId;
        }

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = "Please enter approval remark",
                        FieldType = FieldType.Text,
                        NextField = null,
                    };
            }
            return null;
        }

        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();

            var reconciliation = service.GetReconciliation(this.ReconciliationId);
            if (reconciliation == null)
                throw new InvalidOperationException($"Reconciliation {this.ReconciliationId} is invalid");
            service.AddReconciliationWorkItem(from.Id.ToString(), new ReconciliationWorkItem
            {
                ReconciliationId = this.ReconciliationId,
                WorkType = ReconciliationWorkItem.WORK_TYPE_APPROVE,
                Note = (String)this.FieldData[FIELD_NOTE].Val()
            }, null);
            await bot.SendTextMessageAsync(chatId, "Reconciliation request has been approved.");

            return DialogResult.Terminated;
        }
    }

    public class ReconciliationRejectDialog : FormDialog
    {
        const String FIELD_NOTE = "Note";
        public Guid ReconciliationId { get; set; }
        public override string FirstField => FIELD_NOTE;

        public ReconciliationRejectDialog(ChatId chatId, User from, Guid reconciliationId) : base(chatId, from)
        {
            this.ReconciliationId = reconciliationId;
        }

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = "Please enter rejection remark",
                        FieldType = FieldType.Text,
                        NextField = null,
                    };
            }
            return null;
        }

        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new SmartLedgerService();
            var reconciliation = service.GetReconciliation(this.ReconciliationId);
            if (reconciliation == null)
                throw new InvalidOperationException($"Reconciliation {this.ReconciliationId} is invalid");
            service.AddReconciliationWorkItem(from.Id.ToString(), new ReconciliationWorkItem
            {
                ReconciliationId = this.ReconciliationId,
                WorkType = ReconciliationWorkItem.WORK_TYPE_REJECTED,
                Note = (String)this.FieldData[FIELD_NOTE].Val()
            }, null);
            await bot.SendTextMessageAsync(chatId, "Reconciliation request has been rejected.");

            // Additional logic as needed

            return DialogResult.Terminated;
        }
    }

}
