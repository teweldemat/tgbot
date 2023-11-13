using Microsoft.EntityFrameworkCore;
using TgBot.SmartLedger;

namespace TgBot.Exchange
{
    public class ExchangeDb:TgBotDb
    {
        public ExchangeDb(DbContextOptions<TgBotDb> options) : base(options)
        {

        }

        public DbSet<ExchangeUserProfile> UserProfiles{ get; set; }
        public DbSet<AssetType> AssetTypes { get; set; }
        public DbSet<BankAccountType> BankAccountTypes { get; set; }
        public DbSet<TrusteeInformation> TrusteeInformations { get; set; }
        public DbSet<TrusteeBankAccount> TrusteeBankAccounts { get; set; }
        public DbSet<ExchangeTransaction> ExchangeTransactions { get; set; }
        public DbSet<ExchangeTransactionData> ExchangeTransactionData { get; set; }
        public DbSet<ExchangeOffer> ExchangeOffers { get; set; }
        public DbSet<OfferBankAccount> OfferBankAccounts { get; set; }
        public DbSet<OfferStatusHistory> OfferStatusHistory { get; set; }
        public DbSet<TrusteeApplication> TrusteeApplications { get; set; }
        
        static BankAccountType[] initialBankAccounts()
        {
            return new BankAccountType[] {
            new BankAccountType
            {
                BankId = "CBE",
                Name = "Commercial Bank of Ethiopia",
            },
            new BankAccountType
            {
                BankId = "ABYS",
                Name = "Commercial Bank of Ethiopia",
            },
            new BankAccountType
            {
                BankId = "DASH",
                Name = "Dashen Bank",
            },
            new BankAccountType
            {
                BankId = "CBO",
                Name = "Cooperative Bank of Oromia",
            },
            new BankAccountType
            {
                BankId = "HBR",
                Name = "Bibret Bank",
            },
            new BankAccountType
            {
                BankId = "ZMN",
                Name = "Zemen Bank",
            }
            };
        }
        static AssetType[] initialAssetTypes()
        {
            return new AssetType[]
            {
                new AssetType{ Key="USDT",Name="Tether",MinOffer=100,MaxOffer=1000000,OneUnit=100,MinPrice=1,MaxPrice=1_000_000*100}
                ,new AssetType{ Key="BTC",Name="Bitcoin",MinOffer=100,MaxOffer=1000000,OneUnit=100,MinPrice=1,MaxPrice=1_000_000*100}
                ,new AssetType{ Key="ETH",Name="Etherium",MinOffer=100,MaxOffer=1000000,OneUnit=100,MinPrice=1,MaxPrice=1_000_000*100}
            };

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                modelBuilder.Entity(entityType.ClrType).ToTable(entityType.ClrType.Name);
            }
            modelBuilder.Entity<OfferBankAccount>().HasKey("OfferId", "BankId");
            modelBuilder.Entity<TrusteeBankAccount>().HasKey("TrusteId", "BankId");
            modelBuilder.Entity<BankAccountType>().HasData(initialBankAccounts());
            modelBuilder.Entity<AssetType>().HasData(initialAssetTypes());
        }

    }
}
