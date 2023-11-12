using Microsoft.EntityFrameworkCore;
using Npgsql.Replication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using TgBot.SmartLedger.AccountReconciliation;

namespace TgBot.SmartLedger
{
    public class TgBotService<T>: ServiceBase<T> where T:TgBotDb,new()
    {
        protected delegate void AuditTransactNoReturnDelegate(SmartLedgerDb db, Guid aid);
        protected delegate T AuditTransactionReturnDelegate<T>(SmartLedgerDb db, Guid aid);
        protected delegate void ProcessDeltaBeforeSaveDelegate(Object deltaData);
        protected void AuditTransactNoReturn(String userId, String operation, object deltaData, long now, AuditTransactNoReturnDelegate f)
        {
            var aid = RecordAudit(userId, operation + "_attempt", deltaData, now, null, false);
            using (var db = new SmartLedgerDb())
            {
                var tran = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                try
                {
                    RecordAuditInternal(db, userId, operation, deltaData, now, aid);
                    f(db, aid);
                    db.SaveChanges();
                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    RecordAudit(userId, operation + "_error", Program.GetExceptionData(ex), now, aid, true);
                    throw;
                }
            }
        }

        protected T AuditTransactReturn<T>(String userId, String operation, object data, long now, AuditTransactionReturnDelegate<T> f)
        {
            var aid = RecordAudit(userId, operation + "_attempt", data, now, null, false);
            using (var db = new SmartLedgerDb())
            {
                var tran = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                try
                {
                    RecordAuditInternal(db, userId, operation, data, now, aid);
                    var ret = f(db, aid);
                    db.SaveChanges();
                    tran.Commit();
                    return ret;
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    RecordAudit(userId, operation + "_error", Program.GetExceptionData(ex), now, aid, true);
                    throw;
                }
            }
        }
        Guid RecordAuditInternal(TgBotDb db, String userId, String operation, Object deltaData, long now, Guid? parent)
        {
            var a = new AuditRecord
            {
                Id = Guid.NewGuid(),
                Time = now,
                Operation = operation,
                UserId = userId,
                ParentRecord = parent,
            };
            db.AuditRecords.Add(a);
            if (deltaData != null)
            {
                var delta = new MisDelta
                {
                    AuditId = a.Id,
                    Data = Newtonsoft.Json.JsonConvert.SerializeObject(deltaData),
                    DataType = deltaData.GetType().ToString(),
                    RecordNo = db.DeltaRecords.Count() + 1,
                    Version = 1,
                    Time = now

                };
                db.DeltaRecords.Add(delta);
            }
            db.SaveChanges();
            return a.Id;
        }
        Guid RecordAudit(String userId, String operation, Object deltaData, long now, Guid? parent, bool suppressError)
        {
            try
            {
                return TransactReturn<Guid>(db =>
                {
                    return RecordAuditInternal(db, userId, operation, deltaData, now, parent);
                });
            }
            catch (Exception ex)
            {
                Program.LogException("Error trying to record audit", ex);
                if (suppressError)
                    return Guid.Empty;
                throw;
            }
        }
        public MisUserProfile GetUserProfile(String userId)
            => DbRead(db => db.MisUserProfiles.AsNoTracking().Where(x => x.UserId.Equals(userId)).FirstOrDefault());
        public List<MisUserProfile> GetAllUserProfiles()
            => DbRead(db => db.MisUserProfiles.AsNoTracking().ToList());
        public List<MisUserProfile> GetActiveUserProfiles()
            => DbRead(db => db.MisUserProfiles.Where(x => x.Permitted).AsNoTracking().ToList());
        public void SetPaymentProfile(MisUserProfile profile)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(
                profile.UserId, "SetPaymentProfile", profile, now,
                (db, aid) =>
                {
                    var existing = db.MisUserProfiles.AsNoTracking().Where(x => x.UserId.Equals(profile.UserId)).FirstOrDefault();
                    if (existing == null)
                    {
                        profile.AuditId = aid;
                        profile.Permitted = true;
                        db.MisUserProfiles.Add(profile);
                    }
                    else
                    {
                        profile.AuditId = aid;
                        profile.Permitted = existing.Permitted;
                        db.MisUserProfiles.Update(profile);
                    }
                    db.SaveChanges();
                });
        }
        public CashEntity GetEntity() => DbRead(db => GetEntityInternal(db));
        protected CashEntity GetEntityInternal(T db)
        {
            return db.CashEntities.AsNoTracking().FirstOrDefault();
        }
        public Guid CreateEntity(String userId, String name, String ruleType, String ruleData)
        {
            var now = TGBot.Now();
            return AuditTransactReturn<Guid>(
                userId, "CreateEntity", new { name, ruleType, ruleData }, now,
                (db, aid) =>
                {

                    if (string.IsNullOrEmpty(name))
                        throw new UserFriendlyError("Name must provided");
                    var entity = new CashEntity
                    {
                        Id = Guid.NewGuid(),
                        Owner = userId,
                        AuditId = aid,
                        TransactionHead = null,
                        Name = name
                    };
                    db.CashEntities.Add(entity);
                    var rule = new PaymentFlowRule
                    {
                        EntityId = entity.Id,
                        AuditId = aid,
                        RuleType = ruleType,
                        Rule = ruleData,
                    };
                    db.PaymentFlowRules.Add(rule);
                    db.SaveChanges();
                    return entity.Id;
                });
        }

    }
    public class SmartLedgerService : TgBotService<SmartLedgerDb>
    {
        internal Guid AddCashAccount(string userId, CashAccount cashAccount, Func<Guid, PaymentFlowRule> UpdateConfig)
        {
            var now = TGBot.Now();
            return AuditTransactReturn(
                userId, "AddCashAccount", cashAccount, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");
                    if (!e.Owner.Equals(userId))
                        throw new UserFriendlyError("Only owner of the company can do this");
                    var account = new CashAccount
                    {
                        Id = Guid.NewGuid(),
                        AuditId = aid,
                        Balance = 0,
                        Code = cashAccount.Code,
                        Name = cashAccount.Name,
                    };
                    db.CashAccounts.Add(account);
                    if (UpdateConfig != null)
                    {
                        var config = UpdateConfig(account.Id);
                        var ruleRec = new PaymentFlowRule
                        {
                            EntityId = e.Id,
                            AuditId = aid,
                            Rule = config.Rule,
                            RuleType = config.RuleType
                        };
                        db.PaymentFlowRules.Update(ruleRec);
                    }
                    //begening balance
                    if (cashAccount.Balance != 0)
                    {
                        var t = new Transaction
                        {
                            Id = Guid.NewGuid(),
                            AuditId = aid,
                            Payment = null,
                            Time = now,
                            Remark = $"Beginning balance for {account.Name}"
                        };
                        var le = new CashLedgerEntry
                        {
                            Id = Guid.NewGuid(),
                            AccountId = account.Id,
                            Amount = cashAccount.Balance,
                            Remark = t.Remark,
                            Time = t.Time,
                            TransactionId = t.Id,
                        };
                        db.SaveChanges();
                        db.Entry(account).State = EntityState.Detached;
                        TransactInternal(db, t, new[] { le });
                    }
                    db.SaveChanges();
                    return account.Id;
                });
        }

        public void SetRule(String userId, String ruleType, String ruleData)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(
                userId, "CreateEntity", ruleData, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");
                    if (!e.Owner.Equals(userId))
                        throw new UserFriendlyError("Only owner of the company can do this");
                    SetRuleInternal(db, aid, e, ruleType, ruleData);
                    db.SaveChanges();
                });
        }

        private static void SetRuleInternal(SmartLedgerDb db, Guid aid, CashEntity e, string ruleType, string ruleData)
        {
            var ruleRec = new PaymentFlowRule
            {
                EntityId = e.Id,
                AuditId = aid,
                Rule = ruleData,
                RuleType = ruleType
            };
            db.PaymentFlowRules.Update(ruleRec);
        }

        public PaymentFlowRule GetRule() => DbRead(db => db.PaymentFlowRules.AsNoTracking().FirstOrDefault());
        public T GetRuleData<T>() where T : class
        {
            var r = this.GetRule();
            if (r == null || r.Rule == null)
                return null;
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(r.Rule);
        }
        public Payment GetPayment(Guid paymentId)
            => DbRead(db => GetPaymentInternal(db, paymentId));
        public Payment GetPaymentByRef(String pref)
            => DbRead(db => GetPaymentByRefInternal(db, pref));
        public Reconciliation GetReconciliationByRef(String pref)
            => DbRead(db => GetReconciliationByRefInternal(db, pref));
        private Payment GetPaymentInternal(SmartLedgerDb db, Guid paymentId)
        {
            return db.Payments.AsNoTracking().Where(x => x.Id == paymentId).FirstOrDefault();
        }
        private Reconciliation GetReconciliationInternal(SmartLedgerDb db, Guid reconciliationId)
        {
            return db.Reconciliations.AsNoTracking().Where(x => x.Id == reconciliationId).FirstOrDefault();
        }
        public Reconciliation GetReconciliation(Guid reconciliationId)
            => DbRead(db =>db.Reconciliations.AsNoTracking().Where(x => x.Id == reconciliationId).FirstOrDefault());
        private Payment GetPaymentByRefInternal(SmartLedgerDb db, String pref)
        {
            return db.Payments.AsNoTracking().Where(x => x.Reference.ToLower().Equals(pref.ToLower())).FirstOrDefault();
        }
        private Reconciliation GetReconciliationByRefInternal(SmartLedgerDb db, String pref)
        {
            return db.Reconciliations.AsNoTracking().Where(x => x.Reference.ToLower().Equals(pref.ToLower())).FirstOrDefault();
        }
        public Guid CreatePaymentFlow(String userId,
            String note,
            long amount,
            String payTo,
            Guid? transferTo,
            IList<WorkItemPicture> attachments,
            Guid? restartPayment)
        {
            var now = TGBot.Now();
            return AuditTransactReturn(
                userId, "CreatePaymentFlow", new { note, amount }, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");
                    int max = 0;

                    string reference;
                    Payment existing = null;
                    if (restartPayment == null)
                    {
                        string prefix;
                        if (amount < 0)
                            prefix = Payment.DR_REF_PREFIX;
                        else if (transferTo != null)
                            prefix = Payment.TR_REF_PREFIX;
                        else
                            prefix = Payment.PR_REF_PREFIX;

                        foreach (var pr in db.Payments)
                        {
                            if (pr.Reference.StartsWith(prefix))
                            {
                                var ser = int.Parse(pr.Reference.Substring(prefix.Length));
                                if (ser > max)
                                    max = ser;
                            }
                        }
                        reference = prefix + (max + 1);
                    }
                    else
                    {
                        existing = GetPaymentInternal(db, restartPayment.Value);
                        reference = existing.Reference;
                        foreach (var s in db.PaymentSources.Where(x => x.PaymentId == restartPayment.Value))
                            db.PaymentSources.Remove(s);
                    }


                    Payment p;
                    if (existing == null)
                        p = new Payment
                        {
                            Id = Guid.NewGuid(),
                            Reference = reference,
                            AuditId = aid,
                            Time = now,
                            Amount = amount,
                            Note = note,
                            Creator = userId,
                            ToPayTo = payTo,
                            TransferTo = transferTo,
                        };
                    else
                        p = new Payment
                        {
                            Id = existing.Id,
                            WorkItemHead = existing.WorkItemHead,
                            Time = existing.Time,

                            Reference = reference,
                            AuditId = aid,
                            Amount = amount,
                            Note = note,
                            Creator = userId,
                            ToPayTo = payTo,
                            TransferTo = transferTo
                        };

                    var w = new PaymentWorkItem
                    {
                        Id = Guid.NewGuid(),
                        AuditId = aid,
                        Data = p.PositiveAmount.ToString(),
                        Note = note,
                        PrevItem = p.WorkItemHead,
                        Time = now,
                        UserId = userId,
                        WorkType = PaymentWorkItem.WORK_TYPE_CREATE,
                        PaymentId = p.Id,
                        
                    };
                    p.WorkItemHead = w.Id;
                    p.HeadTime = w.Time;
                    p.HeadType = w.WorkType;
                    if (restartPayment == null)
                        db.Payments.Add(p);
                    else
                        db.Payments.Update(p);
                    db.WorkItems.Add(w);

                    insertAttachments(db, w, attachments);
                    db.SaveChanges();
                    return p.Id;
                });
        }


        public Guid CreateReconciliationFlow(String userId,
            String note,
            Guid accountId,
            long balance,
            long accountBalance,
            IList<WorkItemPicture> attachments)
        {
            var now = TGBot.Now();
            return AuditTransactReturn(
                userId, "CreateAccountReconciliationFlow", new { accountId, balance, note }, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");
                    int max = 0;

                    string reference;

                    string prefix = WorkFlowState.GEN_REF_PREFIX;

                    foreach (var pr in db.Payments)
                    {
                        if (pr.Reference.StartsWith(prefix))
                        {
                            var ser = int.Parse(pr.Reference.Substring(prefix.Length));
                            if (ser > max)
                                max = ser;
                        }
                    }
                    reference = prefix + (max + 1);



                    Reconciliation p;

                    p = new Reconciliation
                    {
                        Id = Guid.NewGuid(),
                        Reference = reference,
                        AuditId = aid,
                        Time = now,
                        AccountId = accountId,
                        Balance = balance,
                        AccountBalance=accountBalance,
                        Note = note,
                        Creator = userId,
                    };

                    var w = new ReconciliationWorkItem
                    {
                        Id = Guid.NewGuid(),
                        AuditId = aid,
                        Data = p.Balance.ToString(),
                        Note = note,
                        PrevItem = p.WorkItemHead,
                        Time = now,
                        UserId = userId,
                        WorkType = ReconciliationWorkItem.WORK_TYPE_REQUEST,
                        ReconciliationId = p.Id,

                    };
                    p.WorkItemHead = w.Id;
                    p.HeadTime = w.Time;
                    p.HeadType = w.WorkType;
                    db.Reconciliations.Add(p);
                    db.ReconciliationWorkItems.Add(w);

                    insertAttachments(db, w, attachments);
                    db.SaveChanges();
                    return p.Id;
                });
        }

        private static void insertAttachments(SmartLedgerDb db, WorkItem w, IList<WorkItemPicture> attachments)
        {
            int n = 1;
            if (attachments == null)
                return;
            foreach (var a in attachments)
            {
                if (a.Image != null && a.Image.Length > 0)
                {
                    if (string.IsNullOrWhiteSpace(a.ImgeMime))
                        throw new UserFriendlyError("Image MIME type not specified");
                }
                if (!string.IsNullOrWhiteSpace(a.LinkedImage))
                    if (string.IsNullOrWhiteSpace(a.LinkedImageType))
                        throw new UserFriendlyError("Linked image type not specified");
                if ((a.Image == null || a.Image.Length == 0)
                    && (string.IsNullOrWhiteSpace(a.LinkedImage)))
                    throw new UserFriendlyError("Image data is empty");

                db.WorkItemPictures.Add(new WorkItemPicture
                {
                    Id = Guid.NewGuid(),
                    Image = a.Image,
                    ImgeMime = a.ImgeMime,
                    LinkedImage = a.LinkedImage,
                    LinkedImageType = a.LinkedImageType,
                    OrderN = n++,
                    WorkItemId = w.Id

                });
            }
        }
        public List<PaymentSource> GetPaymentSources(Guid paymentId)
            => DbRead(db => db.PaymentSources.Where(x => x.PaymentId == paymentId).ToList());



        public Guid AddReconciliationWorkItem(string userId, 
            ReconciliationWorkItem work, 
            IList<WorkItemPicture> attachments = null)
        {
            var now = TGBot.Now();
            return AuditTransactReturn(
                userId, "AddReconciliationWorkItem", work, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");

                    var reconciliation = GetReconciliationInternal(db, work.ReconciliationId);
                    if (reconciliation == null)
                        throw new UserFriendlyError("Invalid reconciliation id: " + work.ReconciliationId);

                    ReconciliationWorkItem w = AddReconciliationWorkItemInternal(db, userId, work, aid, now,reconciliation);

                    reconciliation.WorkItemHead = w.Id;
                    reconciliation.HeadTime = w.Time;
                    reconciliation.HeadType = w.WorkType;
                    var account = this.GetCashAccount(reconciliation.AccountId);
                    reconciliation.AccountBalance = account.Balance;
                    insertAttachments(db, w, attachments);
                    switch (work.WorkType)
                    {
                        case ReconciliationWorkItem.WORK_TYPE_REQUEST:
                            break;
                        case ReconciliationWorkItem.WORK_TYPE_APPROVE:
                            CreateLedgerEntryForReconciliation(db,e,reconciliation, w, account,now, aid);
                            break;
                    }
                    db.Reconciliations.Update(reconciliation);

                    db.SaveChanges();
                    return w.Id;
                });
        }

        private static ReconciliationWorkItem AddReconciliationWorkItemInternal(SmartLedgerDb db, string userId, ReconciliationWorkItem work, Guid aid, 
            long now,Reconciliation reconciliation)
        {
            var w = new ReconciliationWorkItem
            {
                Id = Guid.NewGuid(),
                AuditId = aid,
                Time = now,
                UserId = userId,
                ReconciliationId = work.ReconciliationId,
                Data = work.Data,
                Note = work.Note,
                PrevItem= reconciliation.WorkItemHead,
                WorkType = work.WorkType,
            };
            db.ReconciliationWorkItems.Add(w);
            return w;
        }

        private CashAccount CreateLedgerEntryForReconciliation(SmartLedgerDb db,  CashEntity entity, Reconciliation reconciliation, ReconciliationWorkItem workItem, CashAccount account, long time, Guid aid)
        {
            var t = new Transaction
            {
                Id = Guid.NewGuid(),
                AuditId = aid,
                PrevTransaction = entity.TransactionHead,
                Payment = reconciliation.Id,
                Remark = $"Change for request: {reconciliation.Note}({reconciliation.Reference})"
            };
            var entries = new List<CashLedgerEntry>();
            if(entity.TransactionHead!=null)
            {
                var h = db.CashLedgerEntries.First(e => e.TransactionId== entity.TransactionHead.Value);
                if (h.Time > time)
                    throw new InvalidOperationException("The reconciliation can't be applied as transactions are performed after the reconciliation time");
            }
            
            
            entries.Add(new CashLedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = reconciliation.AccountId,
                Amount = reconciliation.Balance-account.Balance,
                Time = time,
                TransactionId = t.Id,
                Remark = t.Remark
            });

            TransactInternal(db, t, entries);
            return account;
        }


        public Guid AddPaymentWorkItem(String userId,
            PaymentWorkItem work,
            IList<WorkItemPicture> attachments = null,
            IEnumerable<PaymentSource> completePayment = null,
            IEnumerable<PaymentSource> setPaymentSources = null,
            bool reversePayment = false)
        {
            var now = TGBot.Now();
            return AuditTransactReturn(
                userId, "AddWorkItem", work, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");

                    var payment = GetPaymentInternal(db, work.PaymentId);
                    if (payment == null)
                        throw new UserFriendlyError("Invalid payment id: " + work.PaymentId);

                    PaymentWorkItem w = AddPaymentWorkItemInternal(db, userId, work, aid, now, payment);

                    payment.WorkItemHead = w.Id;
                    payment.HeadTime = w.Time;
                    payment.HeadType = w.WorkType;
                    db.Payments.Update(payment);

                    insertAttachments(db, w, attachments);

                    if (setPaymentSources != null)
                    {
                        foreach (var ps in setPaymentSources)
                        {
                            db.PaymentSources.Add(new PaymentSource
                            {
                                PaymentId = payment.Id,
                                Amount = ps.Amount,
                                CashAccountId = ps.CashAccountId,
                                PaymentInstruction = ps.PaymentInstruction
                            });
                        }
                        db.SaveChanges();
                    }
                    if (reversePayment)
                    {
                        var entries = new List<CashLedgerEntry>();
                        var sources = db.PaymentSources.Where(x => x.PaymentId == payment.Id);
                        var paid = new HashSet<String>();
                        ForeEachWorkItemInternal(db, work.PaymentId, x =>
                        {
                            if (!paid.Contains(x.UserId))
                                paid.Add(x.UserId);
                            return true;
                        });
                        var config = this.GetRuleData<SimplePaymentFlowConfiguration>();
                        if (config == null)
                            throw new UserFriendlyError("Configuration not set");
                        var remark = $"Cancelation of payment for request: {payment.Note}({payment.Reference})";
                        var t = new Transaction
                        {
                            Id = Guid.NewGuid(),
                            AuditId = aid,
                            PrevTransaction = e.TransactionHead,
                            Remark = remark,
                            Payment = payment.Id
                        };
                        foreach (var x in sources)
                        {
                            if (paid.Contains(config.GetPayer(x.CashAccountId, payment.IsDeposit)))
                            {
                                entries.Add(new CashLedgerEntry
                                {
                                    Id = Guid.NewGuid(),
                                    AccountId = x.CashAccountId,
                                    Amount = x.Amount,
                                    Time = now,
                                    TransactionId = t.Id,
                                    Remark = remark,

                                });
                            }
                        };

                        TransactInternal(db, t, entries);

                    }
                    if (completePayment != null)
                    {

                        var remark = $"Payment for request: {payment.Note}({payment.Reference})";
                        var t = new Transaction
                        {
                            Id = Guid.NewGuid(),
                            AuditId = aid,
                            PrevTransaction = e.TransactionHead,
                            Payment = payment.Id,
                            Remark = $"Payment for request: {payment.Note}({payment.Reference})"
                        };
                        var entries = new List<CashLedgerEntry>();
                        foreach (var s in completePayment)
                        {
                            entries.Add(new CashLedgerEntry
                            {
                                Id = Guid.NewGuid(),
                                AccountId = s.CashAccountId,
                                Amount = -s.Amount,
                                Time = now,
                                TransactionId = t.Id,
                                Remark = remark
                            });
                            if (payment.IsTransferTransaction)
                            {
                                entries.Add(new CashLedgerEntry
                                {
                                    Id = Guid.NewGuid(),
                                    AccountId = payment.TransferTo.Value,
                                    Amount = s.Amount,
                                    Time = now,
                                    TransactionId = t.Id,
                                    Remark = remark
                                });
                            }
                        }

                        TransactInternal(db, t, entries);

                    }
                    db.SaveChanges();
                    return w.Id;
                });
        }
        
        private static PaymentWorkItem AddPaymentWorkItemInternal(SmartLedgerDb db, string userId, PaymentWorkItem work,
            Guid aid, long now, Payment payment)
        {
            var w = new PaymentWorkItem
            {
                Id = Guid.NewGuid(),
                AuditId = aid,
                Time = now,
                UserId = userId,
                PaymentId = work.PaymentId,
                Data = work.Data,
                Note = work.Note,
                PrevItem = payment.WorkItemHead,
                WorkType = work.WorkType,
            };
            db.WorkItems.Add(w);
            return w;
        }
        

        public List<CashAccount> GetCashAccounts()
        {
            return base.DbRead(db => db.CashAccounts.ToList());
        }
        public CashAccount GetCashAccount(Guid accountId)
        {
            return base.DbRead(db => db.CashAccounts.AsNoTracking().Where(x => x.Id == accountId).FirstOrDefault());
        }
        public int AccountsCount()
        {
            return base.DbRead(db => db.CashAccounts.Count());
        }
        private static CashAccount GetCashAccountInternal(SmartLedgerDb db, Guid accountId)
        {
            return db.CashAccounts.AsNoTracking().Where(x => x.Id == accountId).FirstOrDefault();
        }
        void TransactInternal(SmartLedgerDb db, Transaction t, IEnumerable<CashLedgerEntry> entires)
        {
            var e = GetEntityInternal(db);
            t.PrevTransaction = e.TransactionHead;
            e.TransactionHead = t.Id;
            db.Update(e);

            var balances = new Dictionary<Guid, CashAccount>();
            foreach (var ent in entires)
            {
                CashAccount cash;
                if (balances.ContainsKey(ent.AccountId))
                    cash = balances[ent.AccountId];
                else
                    balances.Add(ent.AccountId, cash = GetCashAccountInternal(db, ent.AccountId));

                if (cash == null)
                    throw new UserFriendlyError("Invalid cash account id:" + ent.AccountId);
                if (cash.Balance + ent.Amount < 0)
                    throw new UserFriendlyError("This transaction will cause a negative balance of " + IntData.toString(cash.Balance + ent.Amount));

                ent.TransactionId = t.Id;
                db.CashLedgerEntries.Add(ent);
                cash.Balance += ent.Amount;
            }
            foreach (var b in balances)
                db.Update(b.Value);
            db.Transactions.Add(t);
            db.SaveChanges();
        }

        internal PaymentWorkItem GetWorkItem(Guid id)
            => DbRead(db =>
             db.WorkItems.AsNoTracking().Where(x => x.Id == id).FirstOrDefault());
        internal ReconciliationWorkItem GetReconciliationWorkItem(Guid id)
            => DbRead(db =>
             db.ReconciliationWorkItems.AsNoTracking().Where(x => x.Id == id).FirstOrDefault());
        internal void ForEachWorkItem(Guid paymentId, Func<PaymentWorkItem, bool> predicate)
        {
            DbReadVoid(db =>
            {
                ForeEachWorkItemInternal(db, paymentId, predicate);
            });
        }

        internal void ForEachReconciliationWorkItem(Guid reconciliationId, Func<ReconciliationWorkItem, bool> predicate)
        {
            DbReadVoid(db =>
            {
                ForEachReconciliationWorkItemInternal(db, reconciliationId, predicate);
            });
        }
        private void ForEachReconciliationWorkItemInternal(SmartLedgerDb db, Guid reconciliationId, Func<ReconciliationWorkItem, bool> predicate)
        {
            var p = GetReconciliationInternal(db, reconciliationId);
            if (p == null)
                return;
            var w = p.WorkItemHead;
            while (w != null)
            {
                var wi = db.ReconciliationWorkItems.Where(x => x.Id == w).FirstOrDefault();
                if (!predicate(wi))
                    return;
                w = wi.PrevItem;
            }
        }
        private void ForeEachWorkItemInternal(SmartLedgerDb db, Guid paymentId, Func<PaymentWorkItem, bool> predicate)
        {
            var p = GetPaymentInternal(db, paymentId);
            if (p == null)
                return;
            var w = p.WorkItemHead;
            while (w != null)
            {
                var wi = db.WorkItems.Where(x => x.Id == w).FirstOrDefault();
                if (!predicate(wi))
                    return;
                w = wi.PrevItem;
            }
        }

        internal List<Payment> GetOpenPayments(int index, int pageSize, out int totalN, bool orderAscending = false,string textFilter=null,bool activeOnly=true)
        {
            int count = 0;
            var ret = DbRead(db =>
              {
                  Func<Payment, bool> filter;

                  if (textFilter == null)
                      filter = x => x.HeadType != PaymentWorkItem.WORK_TYPE_CLOSE && x.HeadType != PaymentWorkItem.WORK_TYPE_CANCELED;
                  else
                  {
                      if (activeOnly)
                      {
                          filter = x => x.HeadType != PaymentWorkItem.WORK_TYPE_CLOSE && x.HeadType != PaymentWorkItem.WORK_TYPE_CANCELED
                              && x.Note.Contains(textFilter);
                      }
                      else
                      {
                          filter = x => x.Note.Contains(textFilter);
                      }
                  }
              
                  count = db.Payments.Where(filter).Count();
                  var res = db.Payments.Where(filter);
                  if (orderAscending)
                      res = res.OrderBy(x => x.Time);
                  else
                      res = res.OrderByDescending(x => x.Time);
                  if (pageSize == -1)
                      return res.ToList();
                  return res
                      .Skip(index).Take(pageSize)
                      .ToList();
              });
            totalN = count;
            return ret;
        }
        internal WorkItemPicture GetPicture(Guid guid)
        => DbRead(db => db.WorkItemPictures.Where(x => x.Id == guid).FirstOrDefault());
        internal List<WorkItemPicture> GetWorkItemPictures(Guid wi)
        => DbRead(db => db.WorkItemPictures.Where(x => x.WorkItemId == wi).OrderBy(x => x.OrderN).ToList());

        public class Ledger
        {
            public class LedgerRecord
            {

                public Transaction transaction;
                public CashLedgerEntry entry;
            }
            public long begningBalance = 0;
            public List<LedgerRecord> record = new List<LedgerRecord>();
        }
        public Ledger GetLedger(Guid accountId, long? fromTime, long? toTime)
        {
            return DbRead(db =>
            {
                var ledger = new Ledger();
                var e = GetEntityInternal(db);
                if (e == null)
                    return ledger;
                var head = e.TransactionHead;
                while (head != null)
                {
                    var t = db.Transactions.Where(x => x.Id == head).First();
                    if (toTime != null && t.Time >= toTime)
                        break;
                    foreach (var le in db.CashLedgerEntries.Where(x => x.TransactionId == head && x.AccountId == accountId))
                    {

                        if (fromTime != null && le.Time < fromTime)
                            ledger.begningBalance += le.Amount;
                        else
                            ledger.record.Insert(0, new Ledger.LedgerRecord
                            {
                                transaction = t,
                                entry = le
                            });
                    }
                    head = t.PrevTransaction;
                }
                return ledger;
            });
        }

        internal void UpdateAccount(string userId, CashAccount account, string configType, string config)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(
                userId, "UpdateAccount", new { account, configType, config }, now,
                (db, aid) =>
                {
                    var e = GetEntityInternal(db);
                    if (e == null)
                        throw new UserFriendlyError("Company information not setup");
                    if (!e.Owner.Equals(userId))
                        throw new UserFriendlyError("Only owner of the company can do this");
                    var existing = GetCashAccountInternal(db, account.Id);
                    if (existing == null)
                        throw new UserFriendlyError("No existing account to update");
                    var newACcount = new CashAccount
                    {
                        Id = existing.Id,
                        AuditId = aid,
                        Balance = existing.Balance,
                        Code = account.Code,
                        Name = account.Name,
                    };
                    db.CashAccounts.Update(account);
                    if (config != null)
                    {
                        SetRuleInternal(db, aid, e, configType, config);
                    }
                    db.SaveChanges();
                });
        }
        public Guid CreateWorkflowInternal(SmartLedgerDb db, String userId, long now, Workflow.WorkFlowInfo workflow, Guid aid)
        {
            var e = GetEntityInternal(db);
            if (e == null)
                throw new UserFriendlyError("Company information not setup");
            var existing = db.WorkFlows.Where(x => x.Reference.ToLower() == workflow.Reference.ToLower()).FirstOrDefault();
            if (existing != null)
                throw new UserFriendlyError($"Workflow code:{workflow.Reference} already used");


            workflow.Creator = userId;
            workflow.Time = now;
            workflow.AuditId = aid;
            workflow.WorkItemHead = null;
            workflow.WorkItemHeadTime = null;
            workflow.WorkItemHeadType = null;
            db.WorkFlows.Add(workflow);
            return workflow.Id;
        }
        private static Workflow.WorkItem AddWorkItemInternal(SmartLedgerDb db, string userId, Workflow.WorkItem workItem,
            Guid aid, long now, Workflow.WorkFlowInfo workflow)
        {
            var w = new Workflow.WorkItem
            {
                Id = Guid.NewGuid(),
                AuditId = aid,
                Time = now,
                UserId = userId,
                WorkFlowId = workItem.Id,
                Data = workItem.Data,
                Note = workItem.Note,
                PrevItem = workflow.WorkItemHead,
                WorkType = workItem.WorkType,
            };
            db.WorkFlowItems.Add(w);
            return w;
        }
    }
}
