using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TgBot.SmartLedger;

namespace TgBot.WeBirrAdmin
{
    public class WeBirrAdminDBService: TgBotService<WeBirrAdminDb>
    {
        public WbaUserProfile GetWbaUserProfile(String userId)
            => DbRead(db =>
             {
                 return new WbaUserProfile
                 {
                     UserId=userId,
                     SuperUser=db.SuperUsers.AsNoTracking().Where(x=>x.UserId==userId).FirstOrDefault(),
                     MerchantPermissions=db.MerchantUsers.AsNoTracking().Where(x=>x.UserId==userId).ToList(),
                 };
             });
    }
}
