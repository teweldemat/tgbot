using Microsoft.EntityFrameworkCore;
using System;
using TgBot.WeTicket;

namespace TgBot.TgDb
{   
    public class TgBotDbContext:DbContext
    {
        public TgBotDbContext(DbContextOptions<TgBotDbContext> options) : base(options)
        {

        }

        public DbSet<TgUserState> WFTelegramUserStates { get; set; }
        public DbSet<JoinedTGGroup> JoinedTGGroups { get; set; }
        public DbSet<WFDialogItem> DialogItems { get; set; }
        
    }
  
}
