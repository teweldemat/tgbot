using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TgBot.SmartLedger;

namespace TgBot.WeBirrAdmin
{
    
    [Table("WbaSuperUser")]
    public class WbaSuperUser
    {
        public string UserId { get; set; }
        public bool WbaAdmin { get; set; } = false;
        public String AdminOfMerchantId { get; set; } = null;
    }
    [Table("WbaMerchantUser")]
    public class WbaMerchantUser
    {
        public String UserId { get; set; }
        public String MerchantId { get; set; }
    }
    public class WbaUserProfile
    {
        public string UserId { get; set; }
        public WbaSuperUser SuperUser { get; set; }
        public List<WbaMerchantUser> MerchantPermissions { get; set; }
        public bool IsSuperUser => SuperUser != null;
        public bool AdminOf(String merchantId) => MerchantPermissions != null && MerchantPermissions.Where(x => x.MerchantId.Equals(merchantId)).Any();
    }
    public class WeBirrAdminDb : TgBotDb
    {
        public DbSet<WbaSuperUser> SuperUsers { get; set; }
        public DbSet<WbaMerchantUser> MerchantUsers { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(Program.GetConnectionString("TGBot"));
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}
