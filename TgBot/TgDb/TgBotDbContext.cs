using Microsoft.EntityFrameworkCore;
using System;
using System.Data.Common;
using TgBot.WeTicket;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TgBot.TgDb
{   
    public class TgBotDbContext:DbContext
    {
        DbConnection _con;
        public TgBotDbContext(DbConnection con) 
        {
            this._con = con;
        }

        public DbSet<TgUserState> WFTelegramUserStates { get; set; }
        public DbSet<JoinedTGGroup> JoinedTGGroups { get; set; }
        public DbSet<WFDialogItem> DialogItems { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_con);
        }

    }
  
}
