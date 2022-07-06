using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TgBot.WFTGDB;
using Microsoft.EntityFrameworkCore;
namespace TgBot.WFDB
{
    public partial class WFDBService: ServiceBase<WFDbContext>
    {
        public WFDBService()
        {
            
        }
        public void AddFRToGroup(long chatId, Guid frid)
        {
            TransactNoReturn((db) => { WFTGDBOps.AddFRToGroup(db, chatId, frid); });
        }
        public void RemoveFRFromGroup(long chatId, Guid frid)
        {
            TransactNoReturn((db) =>
            {
                WFTGDBOps.RemoveFRFromGroup(db, chatId, frid);
            });
        }
        public List<WithDrawalBank> GetAllBanks()
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetAllBanks(db);

        }
        public WithDrawalRequest GetPendingWithdrawalRequest(Guid frid)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetPendingWithdrawalRequest(db, frid);
        }
        public void RequestWithdrawal(Guid frid, Guid agentId, long amount, string note, int bankId, String accountName, String accountNo)
        {
            using (var db = new WFDbContext())
                TransactNoReturn((db) =>
                {
                    WFDBOp.RequestWithdrawal(db, frid, agentId, amount, note,bankId, accountName, accountNo);
                });
        }
        public void ApproveWithdrawal(Guid frid, Guid agentId, String reference, byte[] transferPicture, String transferMime, PictureExternalStorageType ExternalStorageType, String IdInExternalStorage)
        {
            using (var db = new WFDbContext())
                TransactNoReturn((db) =>
                {
                    WFDBOp.ApproveWithdrawal(db, frid, agentId, reference, transferPicture, transferMime,ExternalStorageType,IdInExternalStorage);
                });
        }
        public void RejectWithdrawl(Guid frid, Guid agentId, String rejectionNoe)
        {
            using (var db = new WFDbContext())
                TransactNoReturn((db) =>
                {
                    WFDBOp.RejectWithdrawl(db, frid, agentId, rejectionNoe);
                });

        }

        public void CloseFundRaider(Guid agentID, Guid frid)
        {
            TransactNoReturn((db) => {
                WFDBOp.CloseFundRaider(db, agentID, frid);
            }
            );
        }
        public FundAgent GetAgentByChannel(int channelId, string idInChannel)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetAgentByChannel(db, channelId, idInChannel);
        }
        internal List<FundRaiserPictureItem> GetFundRaiserPictures(Guid guid)
        {
            using(var db=new WFDbContext())
            {
                return WFDBOp.GetFundRaiserPictures(db, guid);
            }
        }
        public  List<FundRaiser> GetUserRaisers(Guid agentId,int index, int count)
        {
            using (var db = new WFDbContext())
                return db.FundRaisers.Where(x => x.AgentID==agentId).OrderByDescending(x => x.StartTime).Skip(index).Take(count).ToList();
        }
        public class FundRaiserWithRequest
        {
            public FundRaiser Fr { get; set; }
            public WithDrawalRequest Request { get; set; }
        }
        public WithDrawalRequest GetTransferRequests(Guid trId)
        {
            using (var db = new WFDbContext())
            {
                var res = db.Withdrawals.AsNoTracking().Where(x => x.Id == trId);
                if (res.Any())
                    return res.First();
                return null;
            }
        }
        public List<FundRaiser> GetTransferRequests(int index, int count)
        {
            using (var db = new WFDbContext())
            {
                var res=db.FundRaisers
                    .Join(db.Withdrawals, fr => fr.Id, w => w.FundRaiserId, (x, y) => new FundRaiserWithRequest
                    {
                        Fr = x,
                        Request = y
                    })
                    .Where(x=>x.Request.ApprovedDate==-1 && x.Request.RejectDate== -1)
                    .Select(x=>x.Fr)
                    .ToList()
                    .OrderByDescending(x => x.StartTime)
                    .Skip(index).Take(count).ToList();
                return res;
            }

        }
        public  List<FundRaiser> GetActiveFundRaisers(int index, int count)
        {
            using (var db = new WFDbContext())
                return db.FundRaisers.Where(x => x.Status == FundRaiserStatus.Active).OrderByDescending(x => x.StartTime).Skip(index).Take(count).ToList();
        }
        public List<FundRaiser> GetGroupFundRaiser()
        {
            using (var db = new WFDbContext())
                return db.FundRaisers.Join(db.FRTGGroups,fr=>fr.Id,g=>g.FundRaisingId,(fr,g)=>fr)
                                        .Where(x => x.Status == FundRaiserStatus.Active).OrderByDescending(x => x.StartTime).ToList();
        }
        public FundRaiser GetFundRaiser(Guid id)
        {

            using (var db = new WFDbContext())
            {
                return WFDBOp.GetFundRaiser(db,id);
            }
        }


        internal FundRaiserStat  GetStat(Guid fundRaiserId)
        {
            using (var db = new WFDbContext())
            {
                return WFDBOp.GetStat(db,fundRaiserId);
            }
        }

        internal static bool IsValidTitle(string text)
        {
            return !String.IsNullOrEmpty(text) && text.Length > 2;
        }
        internal static bool IsValidDescriptoin(string text)
        {
            return !String.IsNullOrEmpty(text) && text.Length > 2;
        }
        internal static bool IsValidAmount(string text,out long amount)
        {
            amount = 0;
            if (!double.TryParse(text, out var d))
                return false;
            if (d < 1)
                return false;
            amount = IntData.toIntMoney(d);
            return true;
        }
        internal FundAgentId GetAgentChannelID(int channelId, Guid agentID)
        {
            using(var db=new WFDbContext())
                return WFDBOp.GetAgentIdByIdInChannel( db, channelId, agentID);
        }
        public FundAgent GetAgent(Guid agentId)
        {
            using (var db = new WFDbContext())
            {
                var res = db.Agents.Where(x => x.Id == agentId);
                if (res.Any())
                    return res.First();
                return null;
            }

        }

        internal void UpdateFundRaiser(FundRaiser fr)
        {
            TransactNoReturn((db) =>
            {
                WFDBOp.UpdateFundRaiser(db, fr);
            });
        }
        public void UpdateFundRaiserPicture( Guid frid, FundRaiserPictureItem picture)
        {
            TransactNoReturn((db) =>
            {
                WFDBOp.UpdateFundRaiserPicture(db, frid,picture);
            });
        }
        internal List<Contribution> GetContributions(Guid frid, int index, int count,out int totalCount)
        {
            using (var db = new WFDbContext())
            {
                totalCount=db.Contributions.Where(x=>x.FundRaiserId==frid).Count();
                return db.Contributions.Where(x => x.FundRaiserId == frid).OrderByDescending(x => x.Time<1?long.MinValue:x.Time).Skip(index).Take(count).ToList();
            }
        }

        internal Contribution GetContribution(Guid cid)
        {
            using (var db = new WFDbContext())
            {
                return db.Contributions.Where(x => x.Id == cid).FirstOrDefault();
            }
        }
        public Guid AddSessionPicture(string mimeType, byte[] data)
        {
            return TransactReturn(db => WFDBOp.AddSessionPicture(db, mimeType, data));
        }
        internal List<long> GetFRGroups(Guid frid)
        {
            using (var db = new WFDbContext())
                return WFTGDBOps.GetFRGroups(db, frid);
        }
    }
}
