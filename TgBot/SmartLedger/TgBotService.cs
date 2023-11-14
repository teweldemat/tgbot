using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TgBot.SmartLedger
{
    public class TgBotService<T>: ServiceBase<T> where T:TgBotDb
    {
        protected delegate void AuditTransactNoReturnDelegate(T db, Guid aid);
        protected delegate RetT AuditTransactionReturnDelegate<RetT>(T db, Guid aid);
        protected delegate void ProcessDeltaBeforeSaveDelegate(Object deltaData);
        public TgBotService(T db):base(db)
        {
        }
        protected void AuditTransactNoReturn(String userId, String operation, object deltaData, long now, AuditTransactNoReturnDelegate f)
        {
            var aid = RecordAudit(userId, operation + "_attempt", deltaData, now, null, false);

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

        protected T AuditTransactReturn<T>(String userId, String operation, object data, long now, AuditTransactionReturnDelegate<T> f)
        {
            var aid = RecordAudit(userId, operation + "_attempt", data, now, null, false);
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
        public virtual Guid CreateEntity(String userId, String name, String ruleType, String ruleData)
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
                    db.SaveChanges();
                    return entity.Id;
                });
        }

    }
}
