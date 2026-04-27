using System;
using System.Collections.Generic;
using System.Linq;
using TgBot.SmartLedger.AccountReconciliation;

namespace TgBot.SmartLedger
{
    public enum SmartLedgerActionKind
    {
        Payment,
        Reconciliation
    }

    public class SmartLedgerActionItem
    {
        public SmartLedgerActionKind Kind { get; set; }
        public Guid Id { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }
        public string PendingAction { get; set; }
        public long Time { get; set; }
    }

    public static class SmartLedgerActionPolicy
    {
        public static IReadOnlyList<string> GetPaymentActions(
            string userId,
            Payment payment,
            PaymentWorkItem head,
            SimplePaymentFlowConfiguration config,
            IEnumerable<PaymentSource> sources,
            IEnumerable<string> paidUserIds)
        {
            var actions = new List<string>();
            if (string.IsNullOrEmpty(userId) || payment == null || head == null || config == null)
                return actions;

            if (head.WorkType == PaymentWorkItem.WORK_TYPE_CREATE && userId.Equals(config.Checker1))
                actions.Add("Check request");

            if (head.WorkType == PaymentWorkItem.WORK_TYPE_CHECK && config.IsApprover(userId))
                actions.Add("Approve request");

            if (head.WorkType == PaymentWorkItem.WORK_TYPE_CANT_PAY && config.IsApprover(userId))
                actions.Add("Resolve declined payment");

            if (head.WorkType == PaymentWorkItem.WORK_TYPE_ACCOUNT && config.IsApprover(userId))
                actions.Add("Close or send back");

            var sourceList = sources?.ToList() ?? new List<PaymentSource>();
            var paidUsers = new HashSet<string>(paidUserIds ?? Enumerable.Empty<string>());
            var totalPaid = GetPaidAmount(config, payment, sourceList, paidUsers);
            var paymentStage = head.WorkType == PaymentWorkItem.WORK_TYPE_APPROVE
                || head.WorkType == PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT
                || head.WorkType == PaymentWorkItem.WORK_TYPE_PAY && totalPaid < payment.PositiveAmount;

            if (paymentStage
                && !paidUsers.Contains(userId)
                && config.IsPayer(userId, sourceList, payment.IsDeposit))
            {
                actions.Add(payment.IsDeposit ? "Receive deposit" : payment.IsTransferTransaction ? "Transfer funds" : "Pay request");
            }

            var accountingStage = head.WorkType == PaymentWorkItem.WORK_TYPE_PAY && !paymentStage
                || head.WorkType == PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING
                || head.WorkType == PaymentWorkItem.WORK_TYPE_REOPEN;

            if (accountingStage && userId.Equals(config.Accountant))
                actions.Add("Account request");

            return actions;
        }

        public static IReadOnlyList<string> GetReconciliationActions(
            string userId,
            Reconciliation reconciliation,
            ReconciliationWorkItem head,
            CashEntity entity)
        {
            var actions = new List<string>();
            if (string.IsNullOrEmpty(userId) || reconciliation == null || head == null)
                return actions;

            if (head.WorkType == ReconciliationWorkItem.WORK_TYPE_REQUEST
                && entity != null
                && userId.Equals(entity.Owner))
            {
                actions.Add("Approve reconciliation");
            }

            if (head.WorkType == ReconciliationWorkItem.WORK_TYPE_REJECTED
                && userId.Equals(reconciliation.Creator))
            {
                actions.Add("Update reconciliation");
            }

            return actions;
        }

        private static long GetPaidAmount(
            SimplePaymentFlowConfiguration config,
            Payment payment,
            IEnumerable<PaymentSource> sources,
            ISet<string> paidUserIds)
        {
            if (config == null || payment == null || sources == null || paidUserIds == null || paidUserIds.Count == 0)
                return 0;

            return sources
                .Where(source => paidUserIds.Contains(config.GetPayer(source.CashAccountId, payment.IsDeposit)))
                .Sum(source => Math.Abs(source.Amount));
        }
    }
}
