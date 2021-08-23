using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Microsoft.EntityFrameworkCore;
namespace TgBot.TgDb
{
    public partial class TgDbService
    {
        public static class DbOps
        {
            public static void JoinedGroup(TgBotDbContext db,String botId,  long groupID)
            {
                var g = GetJoinedTGGroup(db, botId,groupID);
                if (g != null)
                {
                    if (g.LeftTime == -1)
                        return;
                    g.JoinedTime = TGBot.Now();
                    g.LeftTime = -1;
                    db.Update(g);
                    return;
                }
                g = new JoinedTGGroup() {
                    Id=Guid.NewGuid(),  
                    TgGroupId = groupID,
                    BotId = botId, 
                    JoinedTime = TGBot.Now(), 
                    LeftTime = -1 };
                db.Add(g);
            }
            public static void LeftGroup(TgBotDbContext db, String botId, long groupId)
            {
                var g = GetJoinedTGGroup(db,botId, groupId);
                if (g == null)
                    return;
                if (g.LeftTime != -1)
                    return;
                g.LeftTime = TGBot.Now();
                db.Update(g);
                return;
            }
            public static JoinedTGGroup GetJoinedTGGroup(TgBotDbContext db, String botId, long groupId)
            {
                return db.JoinedTGGroups.AsNoTracking().Where(x => x.TgGroupId == groupId && x.LeftTime==-1 && x.BotId==botId).FirstOrDefault();
            }
            public static List<JoinedTGGroup> GetAllJoinedTGGroups(TgBotDbContext db,String botId)
            {
                return db.JoinedTGGroups.AsNoTracking().Where(x=>x.BotId==botId).ToList();
            }
            public static void SaveDialogState(TgBotDbContext db, WFDialogItem stack, IBotDialog dialog)
            {
                var state = GetUserState(db, stack.BotId, stack.TgUserId);
                var tg = GetDialogItem(db, stack.id);
                if (tg == null)
                    throw new Exception("Invalid dialog item id:" + stack.id);                
                tg.DataType = dialog.GetType().ToString();
                tg.Data = Newtonsoft.Json.JsonConvert.SerializeObject(dialog);
                state.LastUpdateTime = TGBot.Now();
                db.Update(state);
                db.Update(tg);
            }
            public static int ClearDialogStack(TgBotDbContext db, String botId,string tgUserId)
            {
                var count = 0;
                var userState = GetUserState(db, botId,tgUserId);                
                var id = userState.StackHead;
                while (id != null)
                {
                    var item = GetDialogItem(db,id.Value);
                    //RemoveDialogItem(db, tgUserId, item.id);
                    count++;
                    id = item.Next;
                }
                userState.StackHead = null;
                db.Update(userState);
                return count;
            }
            public static void RemoveDialogItem(TgBotDbContext db, Guid dialog)
            {
                
                var diag = GetDialogItem(db, dialog);
                if (diag.Removed)
                    return;
                var userState = GetUserState(db, diag.BotId,diag.TgUserId);
                if (dialog == userState.StackHead.Value)
                {
                    userState.StackHead = diag.Next;
                }
                else
                {
                    var res = db.DialogItems.Where(x => x.Next != null && x.Next.Value == dialog);
                    var prev = res.First();
                    prev.Next = diag.Next;
                    db.Update(prev);
                }
                
                diag.Removed = true;
                db.Update(diag);
                
                userState.LastUpdateTime = TGBot.Now();
                db.Update(userState);
                
                db.SaveChanges();
            }
            public static Guid PushDialog(TgBotDbContext db, String botId, String tgUserId, IBotDialog dialog)
            {
                var userState = GetUserState(db,botId, tgUserId);

                var stack = new WFDialogItem
                {
                    id = Guid.NewGuid(),
                    BotId=botId,
                    TgUserId = tgUserId,
                    DataType = dialog.GetType().ToString(),
                    Data = Newtonsoft.Json.JsonConvert.SerializeObject(dialog),
                    Next = userState.StackHead,
                    Time=TGBot.Now()
                };
                userState.StackHead = stack.id;
                userState.LastUpdateTime = TGBot.Now();
                db.Add(stack);
                db.Update(userState);
                return stack.id;
            }
            public static WFDialogItem GetDialogItem(TgBotDbContext db, Guid diagId)
            {
                var res = db.DialogItems.AsNoTracking().Where(x => x.id == diagId);
                if (!res.Any())
                    throw new Exception("Stackhead refered in user state table is invalid");
                return res.First();
            }
            public static TgUserState GetUserState(TgBotDbContext db, String botId,string userID)
            {
                //return DbOps.GetUserState(db,)
                var us = db.WFTelegramUserStates.Where(x => x.TgUserID == userID && x.TgBotId==botId);
                if (us.Any())
                    return us.First();
                return null;

            }
            public static void SetUserState(TgBotDbContext db, String botId,TgUserState user)
            {
                var userState = GetUserState(db, botId,user.TgUserID);
                if (userState == null)
                {
                    userState = new TgUserState()
                    {
                        Id=Guid.NewGuid(),
                        TgBotId=botId,
                        TgUserID = user.TgUserID,
                        LastUpdateTime = TGBot.Now(),
                        Data = null,
                        StackHead = null,
                        WaitingForPayment = false,
                        WbcCode = null,
                    };                    
                    db.Add(userState);
                }
                else
                {
                    userState.LastUpdateTime= TGBot.Now();
                    userState.Data = user.Data;
                    userState.WaitingForPayment = user.WaitingForPayment;
                    userState.WbcCode = user.WbcCode;
                    db.Update(user);
                }
            }

        }
    }
}
