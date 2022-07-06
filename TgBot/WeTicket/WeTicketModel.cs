using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace TgBot.WeTicket
{
    public enum AgentRole
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
        public AgentRole Role { get; set; }
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
    [Table("TicketCompaign")]
    public class TicketCompaign
    {
        [Key]
        public Guid Id { get; set; }
        public Guid AgentID { get; set; }
        public int ChannelID { get; set; }
        public String ShortName { get; set; }
        public String ShortDescription { get; set; }
        public long TicketPrice { get; set; }
        public int MaxTickets { get; set; }
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
        public String FormatTicketSerialNo(int sn)
        {
            int ndigMax = (int)Math.Log10(MaxTickets)+1;
            int ndig = (int)Math.Log10(sn) + 1;
            var sv = new StringBuilder();
            for(var i=ndig;i<ndigMax;i++)
            {
                sv.Append('0');
            }
            sv.Append(sn.ToString());
            return sv.ToString();
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
    [Table("BoughtTicket")]
    public class BoughtTicket
    {
        [Key]
        public Guid Id { get; set; }
        public int SerialNo { get; set; }
        public Guid FundRaiserId { get; set; }
        public int ChannelId { get; set; }
        public bool RegisteredAgent { get; set; }
        public Guid? AgentID { get; set; }
        public bool Anonymous { get; set; }
        public String Alias { get; set; }
        public long Time { get; set; }
        public long Amount { get; set; }
        public String Note { get; set; }
        public String PaymentCode { get; set; }
        
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