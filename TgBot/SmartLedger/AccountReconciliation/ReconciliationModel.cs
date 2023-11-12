using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace TgBot.SmartLedger.AccountReconciliation
{
    [Table("Reconciliation")]
    public class Reconciliation : WorkFlowState
    {
        public Guid AccountId { get; set; }
        public long Balance { get; set; }
    }
    [Table("ReconciliationWorkItem")]
    public class ReconciliationWorkItem : WorkItem
    {
        public const int WORK_TYPE_REQUEST = 1;
        public const int WORK_TYPE_APPROVE = 2;
        public const int WORK_TYPE_REJECTED = 3;
        public const int WORK_TYPE_COMPLETED = 4;

        public Guid ReconciliationId { get; set; }


        public static String StatusString(int workType)
        {
            switch (workType)
            {
                case WORK_TYPE_REQUEST:
                    return "Reconciliation Requested";
                case WORK_TYPE_APPROVE:
                    return "Reconciliation Approved";
                case WORK_TYPE_REJECTED:
                    return "Reconciliation Rejected";
                case WORK_TYPE_COMPLETED:
                    return "Reconciliation Completed";
                default:
                    return "Unknown Status";
            }
        }

        internal static string GetActionString(ReconciliationWorkItem w)
        {
            switch (w.WorkType)
            {
                case WORK_TYPE_REQUEST:
                    return "Requested Reconciliation";
                case WORK_TYPE_APPROVE:
                    return "Approved Reconciliation";
                case WORK_TYPE_REJECTED:
                    return "Rejected Reconciliation";
                case WORK_TYPE_COMPLETED:
                    return "Completed Reconciliation";
                default:
                    return "Unknown Action";
            }
        }
    }
}
