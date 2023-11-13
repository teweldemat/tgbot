using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
namespace TgBot
{
    [Table("TgUserState")]
    public class TgUserState
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public String TgUserID { get; set; }
        public String TgBotId { get; set; }
        [System.ComponentModel.DataAnnotations.DataType(DataType.MultilineText)]
        public String Data { get; set; }
        public long LastUpdateTime { get; set; }
        public Guid? StackHead { get; set; }
        public bool WaitingForPayment { get; set; }
        public string WbcCode { get; set; }
    }
    [Table("JoinedTGGroup")]
    public class JoinedTGGroup
    {
        [Key]
        public Guid Id { get; set; }
        public long TgGroupId { get; set; }
        public String BotId { get; set; }
        public long JoinedTime { get; set; }
        public long LeftTime { get; set; }
    }
    public class DialogData
    {
        public String Type { get; set; }
        public object Dialog { get; set; }
    }
    [Table("WFDialogStack")]
    public class WFDialogItem
    {
        public Guid id { get; set; } = Guid.NewGuid();
        public String TgUserId { get; set; }
        public String BotId { get; set; }
        public Guid? Next { get; set; }
        public String DataType { get; set; }
        public String Data { get; set; }
        public long Time { get; set; }
        public bool Removed { get; set; }
        public IBotDialog Deserialize(IServiceProvider services)
        {
            var t = Type.GetType(DataType);
            if (t == null)
                return null;
            var res = Newtonsoft.Json.JsonConvert.DeserializeObject(Data, t);
            var ret=res as IBotDialog;
            ret.SetServices(services);
            return ret;
        }
    }
}