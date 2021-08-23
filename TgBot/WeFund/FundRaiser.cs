using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace TgBot.WFDB
{
    public enum AgentRol
    {
        GeneralPublic=1,
        Admin=2,
    }
    [Table("FundAgent")]
    public class FundAgent
    {
        [Key]
        public Guid Id { get; set; }
        public String Name { get; set; }
        public AgentRol Role { get; set; }
    }
    [Table("FundAgentId")]
    public class FundAgentId
    {
        [Key]
        public Guid Id { get; set; }
        public Guid AgentId { get; set; }
        public int ChannelId { get; set; }
        public String IdInChannel { get; set; }
        public String NameInChannel { get; set; }        
    }
    [Table("FundRaisingChannel")]
    public class FundRaisingChannel
    {
        public const int CHANNEL_TELEGRAM = 1;
        public const int CHANNEL_WEB = 2;
        [Key]
        public int Id { get; set; }
        public String Description { get; set; }
    }
    public enum FundRaiserStatus
    {
        Active,
        Closed
    }
    [Table("FundRaiser")]
    public class FundRaiser
    {

        [Key]
        public Guid Id { get; set; }
        public Guid AgentID { get; set; }
        public int ChannelID { get; set; }
        public String ShortName { get; set; }
        public String ShortDescription { get; set; }
        public long TargetAmount { get; set; }
        public bool AutoCloseOnTarget { get; set; }
        public long MinAmount { get; set; }
        public long MaxAmount { get; set; }
        public long StartTime { get; set; }
        public long AutoCloseTime { get; set; }
        public long CloseTime { get; set; }
        public FundRaiserStatus Status { get; set; }
        public int CommissionOutTenThousand { get; set; } = 100;
        public int RegistrationFee { get; set; } = 0;
        public object CommssionInPrecent => Math.Round((double)CommissionOutTenThousand / (double)100, 2);

        public long CommssionOf(long amount)
        {
            return (long)Math.Ceiling((double)amount * (double)CommissionOutTenThousand / 10000.0);
        }
    }
    public enum PictureExternalStorageType
    {
        None=0,
        Telegram=1,
    }
    [Table("FundRaiserPictureItem")]
    public class FundRaiserPictureItem
    {
        [Key]
        public Guid Id { get; set; }
        public Guid FundRaiserID { get; set; }
        public int Order;
        public String PictureMIME { get; set; }
        public byte[] Picture { get; set; }
        public PictureExternalStorageType ExternalStorageType { get; set; }
        public String IdInExternalStorage { get; set; }

        public bool HasPicture=> Picture != null && (IdInExternalStorage != null || (Picture==null && Picture.Length > 0 && !String.IsNullOrEmpty(PictureMIME)));
    }
    [Table("Contribution")]
    public class Contribution
    {
        [Key]
        public Guid Id { get; set; }
        public Guid FundRaiserId { get; set; }
        public int ChannelId { get; set; }
        public bool RegisteredAgent { get; set; }
        public Guid? AgentID { get; set; }
        public bool Anonymous { get; set; }
        public String Alias { get; set; }
        public long Time { get; set; }
        public long Amount { get; set; }
        public String Note { get; set; }
        public long ContributionTime { get; set; }
    }
    public class FundRaiserStat
    {
        public Guid FundRaiserId { get; set; }
        public int Count { get; set; } = 0;
        public long Total { get; set; } = 0;
        public int WithdrawalCount { get; set; } = 0;
        public long TotalWithdrawal { get; set; } = 0;
        public long TotalCommission { get; set; } = 0;
        public long TotalRegFee { get; set; } = 0;
    }
    public class WithDrawalBank
    {
        public const int BANK_CBE = 1;
        public int Id{ get; set; }
        public int Order { get; set; }
        public String Name { get; set; } 
    }
    [Table("WithDrawalRequest")]
    public class WithDrawalRequest
    {
        [Key]
        public Guid Id { get; set; }
        public Guid FundRaiserId { get; set; }
        public int BankId { get; set; }
        public String AccountNo { get; set; }
        public String AccountName { get; set; }
        public long Amount { get; set; }
        public long TransferedAmount { get; set; }
        public long RegistrationFee { get; set; }
        public long Comission { get; set; }
        public long RequestDate { get; set; }
        public String RequestNote { get; set; }
        public long ApprovedDate { get; set; }
        public String ApprovalNote { get; set; }
        public String TransferRefernce { get; set; }
        public long RejectDate { get; set; }
        public String RejectNote { get; set; }
        public bool Pending => ApprovedDate == -1 && RejectDate == -1;
    }
    [Table("TansferPicture")]
    public class TansferPicture
    {
        [Key]
        public Guid WithdrawalId { get; set; }
        public byte[] TransferPicture { get; set; }
        public String TransferPictureMime { get; set; }
        public PictureExternalStorageType ExternalStorageType { get; set; }
        public String IdInExternalStorage { get; set; }
    }

    [Table("UserSession")]
    public class UserSession
    {
        public Guid Id { get; set; }
        public long CreateTime { get; set; }
        public Guid UserID{ get; set; }
        public long? CloseTime { get; set; }
        public bool Active => this.CloseTime != null;
    }
    [Table("WebContribution")]
    public class WebContribution
    {
        [Key] 
        public String PaymentCode { get; set; }
        public String ContributionData { get; set; }
        public long? PaidTime { get; set; }
        public long CreateTime { get; set; }
    }
    [Table("WebUser")]
    public class WebUser
    {
        public Guid Id { get; set; }
        public String Email { get; set; }
        public String Name { get; set; }
        public String Password { get; set; }
    }
    [Table("SessionPicture")]
    public class SessionPicture
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }

        public String MimeType { get; set; }
        public byte [] Data { get; set; }
    }
    [Table("FRTGGroup")]
    public class FRTGGroup
    {
        [Key]
        public Guid Id { get; set; }
        public long ChatId { get; set; }
        public long JoinedTime { get; set; }
        public long LeftTime { get; set; }
        public Guid FundRaisingId { get; set; }
    }
}