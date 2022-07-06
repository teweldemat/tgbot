using Microsoft.EntityFrameworkCore;

namespace TgBot.SmartLedger
{
    public class TgBotDb:DbContext
    {
        public DbSet<CashEntity> CashEntities { get; set; }
        public DbSet<AuditRecord> AuditRecords { get; set; }
        public DbSet<MisDelta> DeltaRecords { get; set; }
        public DbSet<MisUserProfile> MisUserProfiles { get; set; }
        public DbSet<Workflow.WorkFlowInfo> WorkFlows { get; set; }
        public DbSet<Workflow.WorkItem> WorkFlowItems { get; set; }
    }
    public class SmartLedgerDb : TgBotDb
    {
        public DbSet<CashAccount> CashAccounts { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<CashLedgerEntry> CashLedgerEntries { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentSource> PaymentSources { get; set; }
        public DbSet<PaymentWorkItem> WorkItems { get; set; }
        public DbSet<WorkItemPicture> WorkItemPictures { get; set; }
        public DbSet<PaymentFlowRule> PaymentFlowRules { get; set; }

        public DbSet<Tasks.MisTask> Tasks { get; set; }
        public DbSet<Tasks.TaskDelta> TaskDeltas { get; set; }
        public DbSet<Tasks.TaskContent> TaskContents { get; set; }
        public DbSet<Tasks.TaskComment> TaskComments { get; set; }
        public DbSet<Tasks.TaskCheckListItem> CheckLists{ get; set; }
        public DbSet<Tasks.TaskUser> TaskUsers { get; set; }
        public DbSet<Tasks.WaitingTask> WaitingTasks { get; set; }
        public DbSet<Tasks.Holiday> Holidays { get; set; }
        public DbSet<Tasks.DutyStationSechedule> DutySchedules { get; set; }
        public DbSet<Tasks.OnDutyCheck> OnDutyChecks { get; set; }
        public DbSet<Tasks.UserTaskType> UserTaskTypes { get; set; }
        public DbSet<Tasks.TaskType> TaksTypes { get; set; }
        public DbSet<Tasks.DutyStation> DutyStations { get; set; }
        public DbSet<Tasks.UserDutyStation> UserDutyStations { get; set; }
        public DbSet<Tasks.UserDutyStationStatus> UserDutyStationStatus { get; set; }
        public DbSet<Tasks.UserDutyStationException> Leaves { get; set; }
        public DbSet<Tasks.TaskEntityConfiguration> TaskConfig { get; set; }
        public DbSet<Tasks.UserWorkState> WorkerStates { get; set; }
        public DbSet<Tasks.FlowReportVersion> FRVersions { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Program.GetConnectionString("TGBot"));
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tasks.TaskType>().HasData(new Tasks.TaskType { Id = 1, Name = "Technical",OrderN=1 });
            modelBuilder.Entity<Tasks.TaskType>().HasData(new Tasks.TaskType { Id = 2, Name = "Administrative", OrderN = 2 });
            modelBuilder.Entity<Tasks.TaskType>().HasData(new Tasks.TaskType { Id = 3, Name = "Business Development", OrderN = 3 });
            modelBuilder.Entity<Tasks.TaskType>().HasData(new Tasks.TaskType { Id = 99, Name = "Others", OrderN = 99 });
            
            modelBuilder.Entity<Tasks.WaitingTask>().HasNoKey();
        }
    }
}
