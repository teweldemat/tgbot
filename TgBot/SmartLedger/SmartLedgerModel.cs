using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using TgBot.SmartLedger.Dialog;
using TgBot.TgDb;
using static TgBot.FormDialog;

namespace TgBot.SmartLedger
{
    [Table("MisDelta")]
    public class MisDelta
    {
        [Key]
        public Guid AuditId { get; set; }
        public long Time { get; set; }
        [MaxLength]
        public String Data { get; set; }
        public String DataType { get; set; }
        public long RecordNo { get; set; }
        public int Version { get; set; }
    }
    [Table("MisUserProfile")]
    public class MisUserProfile
    {
        public enum GenderType
        {
            Male,
            Female
        }
        [Key]
        public String UserId { get; set; }
        public String FullName { get; set; }
        public bool Permitted { get; set; } = false;
        public String ShortName { get; set; }
        public GenderType Gender { get; set; } = GenderType.Male;
        public Guid AuditId { get; set; }
        public String Name() => String.IsNullOrEmpty(ShortName) ? FullName : ShortName;
    }

    public class SimplePaymentFlowConfiguration
    {
        public class AccountPayer
        {
            public Guid AccountId { get; set; }
            public String Payer { get; set; }
            public String Depositor { get; set; }
        }
        public String Checker1 { get; set; }
        public String Approver1 { get; set; }
        public String Accountant { get; set; }
        public List<AccountPayer> Payers { get; set; }
        internal bool IsApprover(string userId)
        {
            return userId.Equals(Approver1);
        }

        internal bool IsPayer(string userId, IEnumerable<PaymentSource> sources, bool deposit)
        {
            if(Payers!=null)
                foreach (var p in Payers)
                    if (userId.Equals(deposit ? p.Depositor : p.Payer) && sources.Where(x => x.CashAccountId == p.AccountId).Any())
                        return true;
            return false;
        }
        internal bool IsPayer(string userId, Guid accountId, bool deposit)
        {
            if(this.Payers!=null)
                foreach (var p in Payers)
                    if (userId.Equals(deposit ? p.Depositor : p.Payer) && accountId == p.AccountId)
                        return true;
            return false;
        }

        internal String GetPayer(Guid cashAccountId, bool deposit)
        {
            if(Payers!=null)
                foreach (var p in Payers)
                    if (cashAccountId == p.AccountId)
                        return deposit ? p.Depositor : p.Payer;
            return null;
        }
        internal void SetPayer(Guid cashAccountId, bool deposit, string payer)
        {
            AccountPayer existing = null;
            if (this.Payers != null)
            {
                foreach (var p in Payers)
                    if (cashAccountId == p.AccountId)
                    {
                        existing = p;
                        break;
                    }
            }
            if(existing!=null)
            {
                if (deposit)
                    existing.Depositor = payer;
                else
                    existing.Payer = payer;
            }
            else
            {
                if (this.Payers == null)
                    this.Payers = new List<AccountPayer>();
                this.Payers.Add(new AccountPayer
                {
                    AccountId = cashAccountId,
                    Depositor = deposit ? payer : null,
                    Payer = deposit ? null : payer
                });
            }
        }

        internal List<Guid> GetAccountsOfPayer(string userId, bool deposit)
        {
            return Payers.Where(x => userId.Equals(deposit ? x.Depositor : x.Payer)).Select(x => x.AccountId).ToList();
        }
    }
    [Table("AuditRecord")]
    public class AuditRecord
    {
        public Guid Id { get; set; }
        public String UserId { get; set; }
        public long Time { get; set; }
        public String Operation { get; set; }
        public Guid? ParentRecord { get; set; }
    }
    [Table("CashEntity")]
    public class CashEntity
    {
        public Guid Id { get; set; }
        public String Owner { get; set; }
        public String Name { get; set; }
        public Guid? TransactionHead { get; set; }
        public Guid AuditId { get; set; }
    }
    [Table("CashAccount")]
    public class CashAccount
    {
        public Guid Id { get; set; }
        public String Name { get; set; }
        public String Code { get; set; }
        public long Balance { get; set; }
        public Guid AuditId { get; set; }
    }

    public enum TransactionReverseRole
    {
        None,
        Reversed,
        Reverse
    }
    [Table("Transaction")]
    public class Transaction
    {
        public Guid Id { get; set; }
        public long Time { get; set; }
        public Guid? PrevTransaction { get; set; }
        public Guid AuditId { get; set; }
        public String Remark { get; set; }
        public Guid? Payment { get; set; }
        public TransactionReverseRole ReverseRole { get; set; } = TransactionReverseRole.None;
    }
    [Table("CashLedgerEntry")]
    public class CashLedgerEntry
    {
        public Guid Id { get; set; }
        public Guid TransactionId { get; set; }
        public Guid AccountId { get; set; }
        public long Time { get; set; }
        public long Amount { get; set; }
        public String Remark { get; set; }
        public int DisplayOrder { get; set; }

    }


    [Table("PaymentFlowRule")]
    public class PaymentFlowRule
    {
        [Key]
        public Guid EntityId { get; set; }
        public String RuleType { get; set; }
        [Column(TypeName = "ntext")]
        public String Rule { get; set; }
        public Guid AuditId { get; set; }
    }
    public abstract class WorkFlowState
    {
        public const string GEN_REF_PREFIX = "W";
        public Guid Id { get; set; }
        public long Time { get; set; }
        public String Note { get; set; }
        public Guid? WorkItemHead { get; set; }
        public long? HeadTime { get; set; }
        public int? HeadType { get; set; }
        public Guid AuditId { get; set; }
        public String Creator { get; set; }
        public String Reference { get; set; }
    }
    public abstract class WorkItem
    {
        public Guid Id { get; set; }
        public Guid? PrevItem { get; set; }
        public String UserId { get; set; }
        public long Time { get; set; }
        public String Note { get; set; }
        public Guid AuditId { get; set; }
        public int WorkType { get; set; }
        [Column(TypeName = "ntext")]
        public String Data { get; set; }
    }
    [Table("Payment")]
    public class Payment:WorkFlowState
    {
        public const String PR_REF_PREFIX = "PR";
        public const String DR_REF_PREFIX = "DR";
        public const String TR_REF_PREFIX = "TR";

        public long Amount { get; set; }
        public String ToPayTo { get; set; }
        public Guid? TransferTo { get; set; }
        [NotMapped]
        public bool IsDeposit => Amount < 0;
        [NotMapped]
        public long DepositAmount => -Amount;

        [NotMapped]
        public long PositiveAmount => Amount < 0 ? -Amount : Amount;
        [NotMapped]
        public bool IsTransferTransaction => TransferTo != null;
        [NotMapped]
        public PaymentType PaymentType => IsDeposit ? PaymentType.Deposit : (IsTransferTransaction ? PaymentType.Transfer : PaymentType.Payment);
    }
    [Table("PaymentSource")]
    public class PaymentSource
    {
        public Guid Id { get; set; }
        public Guid PaymentId { get; set; }
        public Guid CashAccountId { get; set; }
        public long Amount { get; set; }
        public String Reference { get; set; }
        public string PaymentInstruction { get; set; }
        public long PayerFee { get; set; }
        public long PayeeFee { get; set; }

    }
    [Table("PaymentWorkItem")]
    public class PaymentWorkItem:WorkItem
    {
        public const int WORK_TYPE_CREATE = 1;
        public const int WORK_TYPE_CHECK = 2;
        public const int WORK_TYPE_SECOND_CHECK = 3;
        public const int WORK_TYPE_APPROVE = 4;
        public const int WORK_TYPE_SECOND_APPROVE = 5;
        public const int WORK_TYPE_PAY = 6;
        public const int WORK_TYPE_ACCOUNT = 7;
        public const int WORK_TYPE_CLOSE = 8;
        public const int WORK_TYPE_CANCELED = 9;
        public const int WORK_TYPE_CANT_PAY = 10;
        public const int WORK_TYPE_ACCOUNT_REJECT = 11;
        public const int WORK_TYPE_SEND_BACK_TO_PAYMENT = 12;
        public const int WORK_TYPE_SEND_BACK_TO_ACCOUNTING = 13;
        public const int WORK_TYPE_VOID = 15;
        public const int WORK_TYPE_REOPEN = 16;

        public Guid PaymentId {get;set;}
        public static String StatusString(int workType, Payment payment)
        {
            switch (workType)
            {
                case PaymentWorkItem.WORK_TYPE_CREATE:
                    return "Created, Waiting for Checking";
                case PaymentWorkItem.WORK_TYPE_CHECK:
                    return "Checked, Waiting for Approval";
                case PaymentWorkItem.WORK_TYPE_SECOND_CHECK:
                    return "Second Checked, Waiting for Approval";
                case PaymentWorkItem.WORK_TYPE_APPROVE:
                    return $"Approved, Waiting for {Program.lm.payment_verb(payment)}";
                case PaymentWorkItem.WORK_TYPE_SECOND_APPROVE:
                    return $"Second Approved, Waiting for {Program.lm.payment_verb(payment)}";
                case PaymentWorkItem.WORK_TYPE_PAY:
                    return $"{Program.lm.payment_verb(payment, pastAction: true)}, Waiting for Accounting";
                case PaymentWorkItem.WORK_TYPE_ACCOUNT:
                    return "Accounting Finished, Waiting for Closure";
                case PaymentWorkItem.WORK_TYPE_CLOSE:
                    return "Closed";
                case PaymentWorkItem.WORK_TYPE_CANCELED:
                    return "Canceled";
                case PaymentWorkItem.WORK_TYPE_ACCOUNT_REJECT:
                    return "Accounting Rejected, Sent Back for Accounting";
                case PaymentWorkItem.WORK_TYPE_CANT_PAY:
                    return $"{Program.lm.payment_verb(payment, capitalize: true)} Declined";
                case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_ACCOUNTING:
                    return "Sent back to accounting, Waiting for Accounting to be Redone";
                case PaymentWorkItem.WORK_TYPE_SEND_BACK_TO_PAYMENT:
                    return "Sent back to accounting, Waiting for Payment to be Redone";
                case PaymentWorkItem.WORK_TYPE_VOID:
                    return "Payment voided";
                case PaymentWorkItem.WORK_TYPE_REOPEN:
                    return "Payment re-opened";

            }
            return "";
        }

        internal static string GetActionString(PaymentWorkItem w, Payment payment,
            Func<IEnumerable<PaymentSource>> sources,
            Func<SimplePaymentFlowConfiguration> config,
            Func<Guid, CashAccount> cashAccount
            )
        {
            switch (w.WorkType)
            {
                case PaymentWorkItem.WORK_TYPE_CREATE:
                    return "Created";
                case PaymentWorkItem.WORK_TYPE_CHECK:
                    return "Checked";
                case PaymentWorkItem.WORK_TYPE_SECOND_CHECK:
                    return "Second Checked";
                case PaymentWorkItem.WORK_TYPE_APPROVE:
                    return "Approved";
                case PaymentWorkItem.WORK_TYPE_SECOND_APPROVE:
                    return "Second Approved";
                case PaymentWorkItem.WORK_TYPE_PAY:
                    var src = sources().Where(x => config().IsPayer(w.UserId, x.CashAccountId, payment.IsDeposit)).ToList();
                    String ret = "";
                    for (int i = 0; i < src.Count; i++)
                    {
                        if (i == 0)
                            ret = "";
                        else
                            ret += ", ";
                        ret += Program.lm.payment_action(payment, cashAccount(src[i].CashAccountId).Name
                            , payment.TransferTo == null ? null : cashAccount(payment.TransferTo.Value).Name);
                    }
                    return ret;
                case PaymentWorkItem.WORK_TYPE_ACCOUNT:
                    return "Accounting Done";
                case PaymentWorkItem.WORK_TYPE_CLOSE:
                    return "Closed";
                case PaymentWorkItem.WORK_TYPE_CANCELED:
                    return "Canceled";
                case PaymentWorkItem.WORK_TYPE_ACCOUNT_REJECT:
                    return "Accounting Rejected";
                case PaymentWorkItem.WORK_TYPE_CANT_PAY:
                    return "Paid";
                case PaymentWorkItem.WORK_TYPE_VOID:
                    return "Payment voided";
                case PaymentWorkItem.WORK_TYPE_REOPEN:
                    return "Payent Reopened";

            }
            return "Unknown Action Done";
        }
    }
    [Table("WorkItemPicture")]
    public class WorkItemPicture
    {
        
        public Guid Id { get; set; }
        public Guid WorkItemId { get; set; }
        public int OrderN { get; set; }
        public byte[] Image { get; set; }
        public String ImgeMime { get; set; }
        public String LinkedImage { get; set; }
        public String LinkedImageType { get; set; }

        internal ContentData AsContentData()
        {
            
            return new ContentData { Id=Id,ContentLink = LinkedImage, Image = Image, ImageMime = ImgeMime, 
                LinkType=ContentLinkType.Url };
        }
    }

    

   

}
