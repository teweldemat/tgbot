using Microsoft.EntityFrameworkCore;
using System;


namespace TgBot.WeTicket
{
    public class WeTicketDb:DbContext
    {

        //Core
        public DbSet<FundAgent> Agents { get; set; }
        public DbSet<FundAgentId> AgentIds { get; set; }
        public DbSet<FundRaisingChannel> Channels{ get; set; }
        public DbSet<TicketCompaign> Compaigns{ get; set; }
        public DbSet<FundRaiserPictureItem> FundRaiserPictures { get; set; }
        public DbSet<BoughtTicket> BoughtTickets{ get; set; }
        public DbSet<WithDrawalRequest> Withdrawals { get; set; }
        public DbSet<WithDrawalBank> WithdrawalBanks { get; set; }
        public DbSet<TansferPicture> TransferPictures { get; set; }

        public WeTicketDb()
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FundRaisingChannel>().HasData(new FundRaisingChannel { Id = FundRaisingChannel.CHANNEL_TELEGRAM, Description="Telegram" });
            modelBuilder.Entity<WithDrawalBank>().HasData(new WithDrawalBank{ Id = WithDrawalBank.BANK_CBE, Name = "Commercial Bank of Ethiopia",Order=1 });
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Program.GetConnectionString("TGBot"));
        }

    }
}
