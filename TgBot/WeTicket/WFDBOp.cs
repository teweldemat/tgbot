using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TgBot.Controllers;

namespace TgBot.WFDB
{
    public partial class WFDBService
    {
        public static class WFDBOp
        {
            public static List<long> GetFRGroups(WFDbContext db, Guid frid)
            {
                return db.FRTGGroups.Where(x => x.FundRaisingId == frid && x.LeftTime == -1).Select(x => x.ChatId).ToList();
            }
            public static FRTGGroup GetFRGroups(WFDbContext db, long chatId, Guid frid)
            {
                var res = db.FRTGGroups.AsNoTracking().Where(x => x.ChatId == chatId && x.FundRaisingId == frid);
                return res.FirstOrDefault();
            }
            public static void AddFRToGroup(WFDbContext db, long chatId, Guid frid)
            {
                var j = GetJoinedTGGroup(db, chatId);
                if (j == null)
                    JoinedGroup(db, chatId);
                var group = new FRTGGroup
                {
                    ChatId = chatId,
                    JoinedTime = DateTime.Now.Ticks,
                    LeftTime = -1,
                    FundRaisingId = frid,
                };
                var g = GetFRGroups(db, chatId, frid);
                if (g == null)
                {
                    group.Id = Guid.NewGuid();
                    db.Add(group);
                }
                else
                {
                    group.Id = g.Id;
                    db.Update(group);
                }
            }
            public static void RemoveFRFromGroup(WFDbContext db, long chatId, Guid frid)
            {
                var g = GetFRGroups(db, chatId, frid);
                if (g == null)
                    return;
                g.LeftTime = DateTime.Now.Ticks;
                db.Update(g);
            }

            public static List<WithDrawalBank> GetAllBanks(WFDbContext db)
            {
                return db.WithdrawalBanks.AsNoTracking().OrderBy(x => x.Order).ToList();
            }
            public static WithDrawalRequest GetPendingWithdrawalRequest(WFDbContext db, Guid frid)
            {
                var withDrawals = db.Withdrawals.AsNoTracking().Where(x => x.FundRaiserId==frid && x.RejectDate == -1 && x.ApprovedDate==-1);
                if (withDrawals.Any())
                    return withDrawals.First();
                return null;
            }
            public static void RequestWithdrawal(WFDbContext db, Guid frid, Guid agentId, long amount,string note,int bankId,String accountName,String accountNo)
            {
                var stat = GetStat(db, frid);
                var fr = GetFundRaiser(db, frid);
                if (fr == null)
                    throw new Exception("Invalid fund raiser id:" + frid);
                var current = GetPendingWithdrawalRequest(db, frid);
                if(current!=null)
                    throw new UserFriendlyError("There is already a pending withdrawal request, please wait while we finish processing the previous request!");
                var withDrawals = db.Withdrawals.Where(x => x.RejectDate == -1 && x.ApprovedDate==-1);
                
                if(stat.TotalWithdrawal>=stat.Total)
                {
                    throw new UserFriendlyError("The full amount is already withdrawn");
                }
                long commission = (long)Math.Ceiling((double)amount * (double)fr.CommissionOutTenThousand / (double)10000);
                long thisRegFee = 0;
                if(stat.TotalRegFee<fr.RegistrationFee)
                {
                    thisRegFee = Math.Min(fr.RegistrationFee - stat.TotalRegFee, amount-commission);
                }
                long transferAmount = amount - commission - thisRegFee;
                var wreq = new WithDrawalRequest
                {
                    Id=Guid.NewGuid(),
                    FundRaiserId=frid,
                    AccountName=accountName,
                    AccountNo=accountNo,
                    Amount=amount,
                    RequestDate = DateTime.Now.Ticks,
                    RequestNote=note,
                    ApprovalNote =null,
                    ApprovedDate=-1,
                    BankId=bankId,
                    Comission=commission,
                    RegistrationFee=thisRegFee,
                    RejectDate=-1,
                    RejectNote=null,                    
                    TransferedAmount =transferAmount,
                    TransferRefernce=null
                };
                db.Add(wreq);
            }
            public static void ApproveWithdrawal(WFDbContext db, Guid frid, Guid agentId, String reference, byte[] transferPicture, String transferMime
                , PictureExternalStorageType ExternalStorageType,String IdInExternalStorage)
            {
                var current = GetPendingWithdrawalRequest(db, frid);
                if (current == null)
                    throw new Exception("There is no active withdrawal request");
                current.ApprovedDate = DateTime.Now.Ticks;
                current.ApprovalNote = "approved";

                var pic = new TansferPicture
                {
                    WithdrawalId=current.Id,
                    TransferPicture = transferPicture,
                    TransferPictureMime = transferMime,
                    ExternalStorageType=ExternalStorageType,
                    IdInExternalStorage=IdInExternalStorage
                };
                db.Add(pic);
                db.Update(current);
            }
            public static void RejectWithdrawl(WFDbContext db, Guid frid, Guid agentId, String rejectionNoe)
            {
                var current = GetPendingWithdrawalRequest(db, frid);
                if (current == null)
                    throw new Exception("There is no active withdrawal request");
                current.RejectDate = DateTime.Now.Ticks;
                current.RejectNote= rejectionNoe;
                db.Update(current);
            }
            public static void CloseFundRaider(WFDbContext db, Guid agentID, Guid frid)
            {
                var fr = WFDB.WFDBService.WFDBOp.GetFundRaiser(db, frid);
                if (fr.Status != WFDB.FundRaiserStatus.Active)
                    throw new UserFriendlyError("Fund raiser is not active");
                if (fr.AgentID != agentID)
                    throw new UserFriendlyError("Only the user that owns the WeFund can close it");
                fr.Status = FundRaiserStatus.Closed;
                fr.CloseTime = DateTime.Now.Ticks;
                db.Update(fr);
            }
            public static FundAgent GetAgentByChannel(WFDbContext db, int channelId,string idInChannel)
            {
                var res = db.AgentIds.Where(x => x.IdInChannel.Equals(idInChannel) && x.ChannelId == channelId);
                if (res.Any())
                {
                    var id=res.First();
                    return db.Agents.Where(x => x.Id.Equals(id.AgentId)).First();
                }
                return null;
            }
            public static FundAgentId GetAgentIdByIdInChannel(WFDbContext db, int channelId, Guid agentId)
            {
                var res = db.AgentIds.Where(x => x.AgentId.Equals(agentId) && x.ChannelId == channelId);
                if (res.Any())
                {
                    return res.First();
                }
                return null;
            }
            static Guid CreateAgent(WFDbContext db, FundAgent agent,int channlID,String idInChannel,String nameInChannel)
            {
                agent.Id = Guid.NewGuid();
                db.Agents.Add(agent);
                db.AgentIds.Add(new FundAgentId
                {
                    AgentId=agent.Id,
                    ChannelId=channlID,
                    IdInChannel=idInChannel,
                    NameInChannel= nameInChannel
                });
                return agent.Id;
                
            }
            internal static void CreateFundRaiser(WFDbContext db,int channelID, String idInChannel, String nameInChannel, FundRaiser fundRaiser, FundRaiserPictureItem picture)
            {
                if (String.IsNullOrEmpty(fundRaiser.ShortDescription)
                    || fundRaiser.ShortDescription.Length < 1
                    || String.IsNullOrEmpty(fundRaiser.ShortName)
                    || fundRaiser.ShortDescription.Length < 1
                    || fundRaiser.TargetAmount < -1)
                    throw new Exception("Incomplete fundraising information.");

                var agent = GetAgentByChannel(db, channelID, idInChannel);
                if (agent == null)
                {
                    agent = new FundAgent();
                    agent.Name = nameInChannel;
                    agent.Id = CreateAgent(db, agent, channelID, idInChannel, nameInChannel);
                }
                fundRaiser.Id = Guid.NewGuid();
                fundRaiser.ChannelID = channelID;
                fundRaiser.AgentID = agent.Id;
                fundRaiser.StartTime = DateTime.Now.Ticks;
                fundRaiser.AutoCloseOnTarget = false;
                fundRaiser.AutoCloseTime = -1;
                fundRaiser.CloseTime = -1;
                fundRaiser.MinAmount = 0;
                fundRaiser.MaxAmount = 10000;
                db.Add(fundRaiser);
                if(picture!=null && picture.HasPicture)
                {
                    picture.Id = Guid.NewGuid();
                    picture.Order = 1;
                    picture.FundRaiserID = fundRaiser.Id;
                    db.Add(picture);
                }
            }

            internal static Guid AddSessionPicture(WFDbContext db, string mimeType, byte[] data)
            {
                var pic = new SessionPicture();
                pic.Id = Guid.NewGuid();
                pic.Data = data;
                pic.MimeType = mimeType;
                db.SessionPictures.Add(pic);
                return pic.Id;
            }

            public static List<FundRaiser> GetFundRaiser(WFDbContext db, int index,int count)
            {
                return db.FundRaisers.OrderByDescending(x => x.StartTime).Skip(index).Take(count).ToList();
            }
            public static FundRaiser GetFundRaiser(WFDbContext db, Guid fundRaiserID)
            {
                var res = db.FundRaisers.Where(x => x.Id == fundRaiserID);
                if (res.Any())
                    return res.First();
                return null;
            }

            internal static Guid Contribute(WFDbContext db,String userIdInChannel, String nameNamInChannel, Contribution contribution)
            {
                var f = GetFundRaiser(db, contribution.FundRaiserId);
                if (f == null)
                    throw new Exception($"Fund raiser id set to contribution is invalid. {contribution.FundRaiserId}");
                var agent = GetAgentByChannel(db, contribution.ChannelId, userIdInChannel);
                if (agent == null)
                {
                    agent = new FundAgent();
                    agent.Name = nameNamInChannel;
                    agent.Id = CreateAgent(db, agent, contribution.ChannelId, userIdInChannel, nameNamInChannel);
                }
                contribution.Id = Guid.NewGuid();
                contribution.AgentID = agent.Id;
                contribution.Time = DateTime.Now.Ticks;
                db.Contributions.Add(contribution);
                return contribution.Id;
            }

            public static List<FundRaiserPictureItem> GetFundRaiserPictures(WFDbContext db, Guid guid)
            {
                var list = db.FundRaiserPictures.Where(x => x.FundRaiserID == guid).ToList();
                list.Sort((x,y)=>x.Order.CompareTo(y.Order));
                return list;
            }
            public static FundRaiserPictureItem GetFundRaiserPicture(WFDbContext db, Guid id)
            {
                return db.FundRaiserPictures.Where(x => x.Id == id).FirstOrDefault();
            }

            internal static void UpdateFundRaiser(WFDbContext db, FundRaiser fr)
            {
                var existing = GetFundRaiser(db, fr.Id);
                existing.TargetAmount = fr.TargetAmount;
                existing.ShortDescription = fr.ShortDescription;
                existing.ShortName = fr.ShortName;
                db.Update(existing);
            }
            internal static void UpdateFundRaiserPicture(WFDbContext db, Guid frid,FundRaiserPictureItem picture)
            {
                var existing = GetFundRaiser(db, frid);
                if (existing == null)
                    throw new Exception("Invalid fund raiser id");
                var pics=GetFundRaiserPictures(db, frid);
                if(pics.Count==0)
                {
                    picture.FundRaiserID = frid;
                    picture.Order = 1;
                    db.Add(picture);
                }
                else
                {
                    var expic = pics[0];
                    expic.Picture = picture.Picture;
                    expic.PictureMIME = picture.PictureMIME;
                    expic.ExternalStorageType = picture.ExternalStorageType;
                    expic.IdInExternalStorage = picture.IdInExternalStorage;
                    db.Update(expic);
                }
                
            }

            internal static FundRaiserStat GetStat(WFDbContext db, Guid fundRaiserId)
            {
                var resCont = db.Contributions.GroupBy(t => t.FundRaiserId,
                (x, y) =>
                new FundRaiserStat
                {
                    FundRaiserId = x,
                    Count = y.Count(),
                    Total = y.Sum(a => a.Amount)
                }).Where(x => x.FundRaiserId == fundRaiserId);
                FundRaiserStat sumCont=null;
                if (resCont.Any())
                    sumCont=resCont.First();

                var resWithDrawal = db.Withdrawals.Where(x=>x.ApprovedDate!=-1).GroupBy(t => t.FundRaiserId,
                (x, y) =>
                new FundRaiserStat
                {
                    FundRaiserId = x,
                    WithdrawalCount = y.Count(),
                    TotalWithdrawal = y.Sum(a => a.Amount),
                    TotalRegFee = y.Sum(a => a.RegistrationFee),
                    TotalCommission = y.Sum(a => a.Comission)
                }).Where(x => x.FundRaiserId == fundRaiserId);
                FundRaiserStat sumWithdrawal= null;
                if (resWithDrawal.Any())
                    sumWithdrawal = resWithDrawal.First();
                if (sumCont == null && sumWithdrawal != null)
                    return sumWithdrawal;
                if (sumCont != null && sumWithdrawal == null)
                    return sumCont;
                if (sumCont != null && sumWithdrawal != null)
                {
                    sumWithdrawal.Count = sumCont.Count;
                    sumWithdrawal.Total = sumCont.Total;
                    return sumWithdrawal;
                }
                return new FundRaiserStat { FundRaiserId = fundRaiserId };
            }

            public static WithDrawalBank GetWithDrawalBank(WFDbContext db, int bankID)
            {
                var res = db.WithdrawalBanks.AsNoTracking().Where(x => x.Id == bankID);
                if (res.Any())
                    return res.First();
                return null;
            }
            public static Guid RegiserWebUser(WFDbContext db, WebUser user)
            {
                var existing = GetWebUserByEmail(db,user.Email);
                if (existing != null)
                    throw new Exception("Email already used");
                var agent = new WFDB.FundAgent();
                user.Id = Guid.NewGuid();
                db.WebUsers.Add(user);
                agent.Name = user.Name;
                agent.Role = AgentRole.GeneralPublic;
                CreateAgent(db, agent, FundRaisingChannel.CHANNEL_WEB, user.Email, user.Name);
                return user.Id;
            }
            public static WebUser GetWebUserByEmail(WFDbContext db, string email)
                => db.WebUsers.AsNoTracking().Where(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            public static UserSession Login(WFDbContext db, String email, String password)
            {
                var user = GetWebUserByEmail(db,email);
                if (user == null || !user.Password.Equals(password))
                    throw new Exception("Invalid username or password");
                var session = new UserSession();
                session.Id = Guid.NewGuid();
                session.UserID = user.Id;
                session.CreateTime = DateTime.Now.Ticks;
                session.CloseTime = null;
                db.Sessions.Add(session);
                return session;
            }
            public static UserSession GetUserSession(WFDbContext db, Guid id)
                => db.Sessions.AsNoTracking().Where(x => x.Id==id)
                    .FirstOrDefault();

            public static Guid RegisterFundRaiser(WFDbContext db, FundRaiser fr, List<Guid> pictures)
            {

                fr.Id = Guid.NewGuid();
                fr.StartTime = DateTime.Now.Ticks;
                fr.AutoCloseOnTarget = false;
                fr.AutoCloseTime = -1;
                fr.CloseTime = -1;
                fr.MinAmount = 0;
                fr.MaxAmount = -1;
                db.Add(fr);
                var order = 1;
                var spictures = db.SessionPictures.AsNoTracking().Join(pictures, p => p.Id, p => p, (x, y) => x);
                foreach (var pic in spictures)
                {
                    var pi = new FundRaiserPictureItem
                    {

                        Id = pic.Id,
                        FundRaiserID = fr.Id,
                        ExternalStorageType = PictureExternalStorageType.None,
                        Order = (order++),
                        Picture = pic.Data,
                        PictureMIME = pic.MimeType
                    };
                    db.FundRaiserPictures.Add(pi);
                }
                return fr.Id;
            }

            internal static async Task<string> CreatePaymentCodeForContribution(WFDbContext db, Contribution contribution)
            {
                var f = GetFundRaiser(db, contribution.FundRaiserId);
                if (f == null)
                    throw new Exception($"Fund raiser id set to contribution is invalid. {contribution.FundRaiserId}");
                String name;
                String code=null;
                if (contribution.RegisteredAgent)
                {
                    var agent = db.Agents.Where(x => x.Id == contribution.AgentID.Value).FirstOrDefault();
                    if (agent == null)
                        throw new Exception("Invalid agent id");
                    name = agent.Name;
                    code = agent.Id.ToString();
                }
                else
                {
                    name = contribution.Alias;
                    code = null;
                }
                contribution.Id = Guid.NewGuid();
                contribution.Time = DateTime.Now.Ticks;
                
                //db.Contributions.Add(contribution);
                
                var wbc=await WFTGDB.WFTGDBService.WBCheckOutAPI.CheckOut(name,code,"Contribution to "+f.ShortName,contribution.Amount);
                db.UnconfirmedContributions.Add(new WebContribution
                {
                    ContributionData=Newtonsoft.Json.JsonConvert.SerializeObject(contribution),
                    PaymentCode=normalizeCheckOutRef(wbc),
                    PaidTime=null,
                    CreateTime=DateTime.Now.Ticks,
                });
                return wbc;
            }
            public static string normalizeCheckOutRef(String paymentCode)
            {
                var sb = new System.Text.StringBuilder();
                if (paymentCode == null)
                    return null;
                foreach (var ch in paymentCode)
                {
                    if (Char.IsDigit(ch))
                        sb.Append(ch);
                }
                return sb.ToString();
            }
            internal async static Task<bool> CheckContributionPayment(WFDbContext db, string paymentCode)
            {

                var cont = db.UnconfirmedContributions.AsNoTracking().Where(x => x.PaymentCode.Equals(normalizeCheckOutRef(paymentCode))).FirstOrDefault();
                if (cont == null)
                    throw new Exception("Payment code not avialable");
                if (cont.PaidTime != null)
                {
                    return true;
                }
                var res = await WFTGDB.WFTGDBService.WBCheckOutAPI.CheckPayment(paymentCode);
                if(res)
                {
                    cont.PaidTime = DateTime.Now.Ticks;
                    db.Update(cont);
                    db.Contributions.Add(Newtonsoft.Json.JsonConvert.DeserializeObject<Contribution>(cont.ContributionData));
                }
                return res;
            }
            public static SessionPicture GetSessionPicture(WFDbContext db, Guid id)
            {
                return db.SessionPictures.Where(x => x.Id == id).FirstOrDefault();
            }
        }

        internal SessionPicture GetSessionPicture(Guid id)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetSessionPicture(db, id);
        }
        internal FundRaiserPictureItem GetFundRaiserPicture(Guid id)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetFundRaiserPicture(db, id);
        }



        internal async Task<bool> CheckContributionPayment(string paymentCode)
        {
            return await TransactReturn(db => WFDBOp.CheckContributionPayment(db, paymentCode));
        }

        internal async Task<String> CreatePaymentCodeForContribution(Contribution contribution)
        {
            return await TransactReturn(db => WFDBOp.CreatePaymentCodeForContribution(db, contribution));
        }

        public Guid RegisterFundRaiser(FundRaiser fr, List<Guid> pictures)
        {
            return TransactReturn(db => WFDBOp.RegisterFundRaiser(db, fr, pictures));
        }

        internal UserSession GetUserSession(Guid guid)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetUserSession(db, guid);

        }

        public UserSession Login(String email,String password)
        {
            return TransactReturn(db => WFDBOp.Login(db, email,password));
        }

        public Guid RegiserWebUser(WebUser user)
        {
            return TransactReturn(db => WFDBOp.RegiserWebUser(db, user));
        }

        internal WebUser GetWebUserByEmail(string email)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetWebUserByEmail(db, email);
        }

        internal WFDB.WithDrawalBank GetWithDrawalBank(int bankID)
        {
            using (var db = new WFDbContext())
                return WFDBOp.GetWithDrawalBank(db, bankID);
        }
    }
}
