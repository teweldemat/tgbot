using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace TgBot.SmartLedger
{
    public class TgBotDb:DbContext
    {
        DbConnection _con;
        public TgBotDb(DbConnection con)
        {
            this._con = con;
        }
        public DbSet<CashEntity> CashEntities { get; set; }
        public DbSet<AuditRecord> AuditRecords { get; set; }
        public DbSet<MisDelta> DeltaRecords { get; set; }
        public DbSet<MisUserProfile> MisUserProfiles { get; set; }
        public DbSet<Workflow.WorkFlowInfo> WorkFlows { get; set; }
        public DbSet<Workflow.WorkItem> WorkFlowItems { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_con);
        }

    }
}
