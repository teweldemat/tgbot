using Microsoft.EntityFrameworkCore;
using System;
namespace TgBot
{
    public class ServiceBase<DbType> where DbType:DbContext,new()
    {
        protected delegate void TransactNoReturnDelegate(DbType db);
        protected delegate T TransactionReturnDelegate<T>(DbType db);
        protected T DbRead<T>(Func<DbType, T> operation)
        {
            using (var db = new DbType())
                return operation(db);
        }
        protected void DbReadVoid(Action<DbType> operation)
        {
            using (var db = new DbType())
                operation(db);
        }
        protected void TransactNoReturn(TransactNoReturnDelegate f)
        {
            using (var db = new DbType())
            {
                var tran = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                try
                {
                    f(db);
                    db.SaveChanges();
                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
            }
        }
        protected T TransactReturn<T>(TransactionReturnDelegate<T> f)
        {
            using (var db = new DbType())
            {
                var tran = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                try
                {
                    var ret = f(db);
                    db.SaveChanges();
                    tran.Commit();
                    return ret;
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
            }
        }
        
    }
}
