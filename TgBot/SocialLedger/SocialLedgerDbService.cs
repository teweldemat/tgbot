using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using TgBot.SmartLedger;

namespace TgBot.SocialLedger
{
    [Table("LedgerPair")]
    public class LedgerPair
    {
        public Guid Id { get; set; }
        public String UserOne { get; set; }
        public String UserTwo{ get; set; }
        public String FullName { get; set; }
        public long CreatedOn { get; set; }
        public long Balance { get; set; }
        public long? AcceptedOn { get; set; }
        public long? DeclinedOn { get; set; }
    }
    [Table("Transaction")]
    public class SLTransaction
    {
        public Guid Id { get; set; }
        public Guid LedgerId { get; set; }
        public long Time { get; set; }
        public String Creator { get; set; }
        public String Remark { get; set; }
        public int Currency { get; set; }
        public long Amount { get; set; }
        public long? ApprovedOn { get; set; }
        public long? DeclinedOn { get; set; }
        public String DeclineMessage { get; set; }
        public long? CanceledOn { get; set; }
        public String CancelMessage{ get; set; }
        public String ResendMesasage { get; set; }
    }
    public class TransactionCurrency
    {
        public int Id { get; set; }
        public String Name { get; set; }
    }
    [Table("TransactionPicture")]
    public class SLTransactionPictures
    {
        public Guid Id { get; set; }
        public Guid TransactionId { get; set; }
        public int OrderN { get; set; }
        public byte[] Image { get; set; }
        public String ImgeMime { get; set; }
        public String LinkedImage { get; set; }
        public String LinkedImageType { get; set; }
    }
    public class SocialLedgerDb:DbContext
    {
        public SocialLedgerDb(DbContextOptions<SocialLedgerDb> options) : base(options)
        {

        }

        public DbSet<LedgerPair> Ledgers { get; set; }
        public DbSet<SLTransaction> Transactions { get; set; }
        public DbSet<SLTransactionPictures> TransactionPictures { get; set; }
        public DbSet<TransactionCurrency> Currencies{ get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionCurrency>().HasData(new TransactionCurrency{ Id = 1,Name = "USD" });
            modelBuilder.Entity<TransactionCurrency>().HasData(new TransactionCurrency { Id = 2, Name = "EUR" });
            modelBuilder.Entity<TransactionCurrency>().HasData(new TransactionCurrency { Id = 3, Name = "Birr" });
        }
        
    }
    public class SocialLedgerDbService:ServiceBase<SocialLedgerDb>
    {
        public SocialLedgerDbService(SocialLedgerDb db) : base(db)
        {
        }

        public LedgerPair GetLedgerPair(Guid id) => DbRead(db => db.Ledgers.AsNoTracking().Where(x => x.Id == id).FirstOrDefault());

        public Guid CreateRequest(String userId,String fullName)
        {
            return base.TransactReturn(db =>
            {
                var pair = new LedgerPair
                {
                    Id = Guid.NewGuid(),
                    AcceptedOn = null,
                    Balance = 0,
                    CreatedOn = TGBot.Now(),
                    DeclinedOn = null,
                    FullName = fullName,
                    UserOne = userId,
                    UserTwo = null
                };
                db.Ledgers.Add(pair);
                return pair.Id;
            });
        }
        public void AcceptRequest(Guid requestId, string userId)
        {
            TransactNoReturn(db =>
            {
                var request = db.Ledgers.AsNoTracking().Where(x => x.Id == requestId).FirstOrDefault();
                if (request == null)
                    throw new UserFriendlyError("Request doesn't exisit");
                if (request.AcceptedOn != null || request.DeclinedOn != null)
                    throw new UserFriendlyError("Request can't be accepted");
                request.UserTwo = userId;
                request.AcceptedOn = TGBot.Now();
                db.Ledgers.Update(request);
            });

        }
        internal void DeclineRequest(Guid requestId, string userId)
        {
            TransactNoReturn(db =>
            {
                var request = db.Ledgers.AsNoTracking().Where(x => x.Id == requestId).FirstOrDefault();
                if (request == null)
                    throw new UserFriendlyError("Request doesn't exisit");
                if (request.AcceptedOn != null || request.DeclinedOn != null)
                    throw new UserFriendlyError("Request can't be declined");
                request.DeclinedOn = TGBot.Now();
                db.Ledgers.Update(request);
            });
        }
        public void RequestTransaction(String userOne,String userTwo, int currency, long amount,String description,
            IEnumerable<SLTransactionPictures> pictures)
        {
            var now = TGBot.Now();
            TransactNoReturn(db =>
            {
                var pair = db.Ledgers.Where(x => (x.UserOne.Equals(userOne) && x.UserTwo.Equals(userTwo))
                  || (x.UserTwo.Equals(userOne) && x.UserOne.Equals(userTwo))
                ).FirstOrDefault();
                if (pair == null)
                    throw new UserFriendlyError("Ledger not found");
                var self = pair.UserOne.Equals(userOne);
                if (!self)
                    amount = -amount;
                var tran = new SLTransaction
                {
                    Id=Guid.NewGuid(),
                    LedgerId = pair.Id,
                    Time=now,
                    Amount = amount,
                    Currency = currency,
                    Creator = userOne,
                    ApprovedOn =null,
                    CanceledOn=null,
                    CancelMessage=null,                    
                    DeclinedOn=null,
                    DeclineMessage=null,                    
                    Remark=description,
                };
                db.Transactions.Add(tran);
                int n = 1;
                foreach (var pic in pictures)
                {
                    pic.Id = Guid.NewGuid();
                    pic.TransactionId = tran.Id;
                    pic.OrderN = n++;
                    db.TransactionPictures.Add(pic);
                }
            });
        }
        public void AcceptTransaction(String userId,Guid tranId)
        {
            var now = TGBot.Now();
            TransactNoReturn(db =>
            {
                var tran=db.Transactions.Where(x => x.Id == tranId).FirstOrDefault();
                if (tran == null)
                    throw new UserFriendlyError("Invalid transaciton id");
                if (tran.ApprovedOn != null
                    || tran.DeclinedOn != null
                    || tran.CanceledOn != null
                )
                    throw new UserFriendlyError("This transaction can't be accepted");
                var pair = db.Ledgers.Where(x => x.Id == tran.LedgerId).First();
                if (tran.Creator.Equals(userId)
                    || (pair.UserTwo.Equals(userId) && !tran.Creator.Equals(pair.UserOne))
                    || (pair.UserOne.Equals(userId) && !tran.Creator.Equals(pair.UserTwo))
                    )
                    throw new UserFriendlyError("This tranaction can't be accepted by this user");
                tran.ApprovedOn = now;                
                pair.Balance += tran.Amount;
            });
        }
        public void DeclineTransaction(String userId, Guid tranId,String note)
        {
           var now = TGBot.Now();
            TransactNoReturn(db =>
            {
                var tran = db.Transactions.Where(x => x.Id == tranId).FirstOrDefault();
                if (tran == null)
                    throw new UserFriendlyError("Invalid transaciton id");
                if (tran.ApprovedOn != null
                    || tran.DeclinedOn != null
                    || tran.CanceledOn != null
                )
                    throw new UserFriendlyError("This transaction can't be accepted");
                var pair = db.Ledgers.Where(x => x.Id == tran.LedgerId).First();
                if ( tran.Creator.Equals(userId)
                    || (pair.UserTwo.Equals(userId) && !tran.Creator.Equals(pair.UserOne))
                    || (pair.UserOne.Equals(userId) && !tran.Creator.Equals(pair.UserTwo))
                    )
                    throw new UserFriendlyError("This tranaction can't be accepted by this user");
                tran.DeclinedOn = now;
                tran.DeclineMessage = note;
            });
        }
        public void CancelTransaction(String userId, Guid tranId)
        {
            var now = TGBot.Now();
            TransactNoReturn(db =>
            {
                var tran = db.Transactions.Where(x => x.Id == tranId).FirstOrDefault();
                if (tran == null)
                    throw new UserFriendlyError("Invalid transaciton id");
                if (tran.ApprovedOn != null
                    || tran.DeclinedOn == null
                    || tran.CanceledOn != null
                )
                    throw new UserFriendlyError("This transaction can't be accepted");
                var pair = db.Ledgers.Where(x => x.Id == tran.LedgerId).First();
                if (!tran.Creator.Equals(userId)
                    || (pair.UserTwo.Equals(userId) && !tran.Creator.Equals(pair.UserOne))
                    || (pair.UserOne.Equals(userId) && !tran.Creator.Equals(pair.UserTwo))
                    )
                    throw new UserFriendlyError("This tranaction can't be accepted by this user");
                tran.CanceledOn = now;
            });
        }
        public void ResendTransaction(String userId, Guid tranId,String note)
        {
            var now = TGBot.Now();
            TransactNoReturn(db =>
            {
                var tran = db.Transactions.Where(x => x.Id == tranId).FirstOrDefault();
                if (tran == null)
                    throw new UserFriendlyError("Invalid transaciton id");
                if (tran.ApprovedOn != null
                    || tran.DeclinedOn == null
                    || tran.CanceledOn != null
                )
                    throw new UserFriendlyError("This transaction can't be accepted");
                var pair = db.Ledgers.Where(x => x.Id == tran.LedgerId).First();
                if (!tran.Creator.Equals(userId)
                    || (pair.UserTwo.Equals(userId) && !tran.Creator.Equals(pair.UserOne))
                    || (pair.UserOne.Equals(userId) && !tran.Creator.Equals(pair.UserTwo))
                    )
                    throw new UserFriendlyError("This tranaction can't be accepted by this user");
                tran.DeclinedOn = null;
                tran.ResendMesasage = note;
            });
        }

    }
}
