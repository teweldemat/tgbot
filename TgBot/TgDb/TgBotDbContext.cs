using Microsoft.EntityFrameworkCore;
using System;

namespace TgBot.TgDb
{   
    public class TgBotDbContext:DbContext
    {
        public DbSet<TgUserState> WFTelegramUserStates { get; set; }
        public DbSet<JoinedTGGroup> JoinedTGGroups { get; set; }
        public DbSet<WFDialogItem> DialogItems { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(Program.GetConnectionString("TGBot"));
        }
    }
  
}
