using Microsoft.EntityFrameworkCore;
using System;
namespace TgBot
{
    public class ServiceBase<DbType> where DbType:DbContext
    {
        protected delegate void TransactNoReturnDelegate(DbType db);
        protected delegate T TransactionReturnDelegate<T>(DbType db);
        protected DbType db;
        public ServiceBase(DbType db)
        {
            this.db = db;
        }

        protected T DbRead<T>(Func<DbType, T> operation)
        {
            return operation(db);
        }
        protected void DbReadVoid(Action<DbType> operation)
        {
            operation(db);
        }
        protected void TransactNoReturn(TransactNoReturnDelegate f)
        {
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
