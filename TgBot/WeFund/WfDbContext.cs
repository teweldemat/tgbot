using Microsoft.EntityFrameworkCore;
using System;
using TgBot.TTDB;
using TgBot.WFDB;

namespace TgBot.WeFund
{
    public class WFDbContext:DbContext
    {

        //Core
        public DbSet<FundAgent> Agents { get; set; }
        public DbSet<FundAgentId> AgentIds { get; set; }
        public DbSet<FundRaisingChannel> Channels{ get; set; }
        public DbSet<FundRaiser> FundRaisers { get; set; }
        public DbSet<FundRaiserPictureItem> FundRaiserPictures { get; set; }
        public DbSet<Contribution> Contributions { get; set; }
        public DbSet<WithDrawalRequest> Withdrawals { get; set; }
        public DbSet<WithDrawalBank> WithdrawalBanks { get; set; }
        public DbSet<TansferPicture> TransferPictures { get; set; }

        //TGBot
        
        
        public DbSet<WFTGDB.FRTGGroup> FRTGGroups { get; set; }
        

        //TT
        public DbSet<PenalityRank> PeanlityRanks { get; set; }
        public DbSet<DrivingLicense> DrivingLicenses { get; set; }
        public DbSet<Ticket> Tickets { get; set; }


        //web
        public DbSet<WebUser> WebUsers { get; set; }
        public DbSet<UserSession> Sessions { get; set; }
        public DbSet<SessionPicture>  SessionPictures { get; set; }
        public DbSet<WebContribution> UnconfirmedContributions{ get; set; }

        public WFDbContext()
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TgUserState>().Property(x => x.Data).HasConversion
                (
                    v => v == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(v),
                    dbv => dbv is String ? Newtonsoft.Json.JsonConvert.DeserializeObject<TgUserState.StateData>(dbv) : null
                );
            modelBuilder.Entity<FundRaisingChannel>().HasData(new FundRaisingChannel { Id = FundRaisingChannel.CHANNEL_TELEGRAM, Description="Telegram" });
            modelBuilder.Entity<WithDrawalBank>().HasData(new WithDrawalBank{ Id = WithDrawalBank.BANK_CBE, Name = "Commercial Bank of Ethiopia",Order=1 });

            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank {Rank=1,PenalityAmount=100,Description="Rank 1" });
            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank { Rank = 2, PenalityAmount = 1500000, Description = "Rank 2" });
            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank { Rank = 3, PenalityAmount = 2000000, Description = "Rank 3" });
            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank { Rank = 4, PenalityAmount = 2500000, Description = "Rank 4" });
            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank { Rank = 5, PenalityAmount = 3000000, Description = "Rank 5" });
            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank { Rank = 6, PenalityAmount = 4000000, Description = "Rank 6" });
            modelBuilder.Entity<TTDB.PenalityRank>().HasData(new TTDB.PenalityRank { Rank = 7, PenalityAmount = 7000000, Description = "Rank 7" });
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Program.GetConnectionString("TGBot"));
        }

    }
}
