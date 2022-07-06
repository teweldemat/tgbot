using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using static TgBot.FormDialog;

namespace TgBot.Exchange
{
    public enum UserStatus
    {
        PendingApproval,
        Approved,
        Blocked,
    }
    public class ExchangeUserProfile
    {
        public enum GenderType
        {
            Male,
            Female
        }
        [Key]
        public String UserId { get; set; }
        public Guid TranId { get; set; }
        public String FirstName { get; set; }
        public String LastName { get; set; }
        public String ShortName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public GenderType Gender { get; set;}
        public String EMail { get; set; }
        public bool EmailVarified { get; set; }
        public UserStatus UserStatus { get; set; }
    }
    public class AssetType
    {
        [Key]
        public String Key { get; set; }
        public String Name { get; set; }
        public long OneUnit { get; set; }
        public long MinPrice { get; set; }
        public long MaxPrice { get; set; }
        public long MinOffer { get; set; }
        public long MaxOffer { get; set; }
    }
    public class ExchangePair
    {
        public String PairKey { get; set; }
        public String SellKey { get; set; }
        public String ByKey { get; set; }
    }
    public class BankAccountType
    {
        [Key]
        public String BankId { get; set; }
        public String Name { get; set; }
        public int? WeeklyTransactionCountLimit { get; set; } = -11;
        public long? SingleTransactionAmountLimit { get; set; } = -1;
        public long? WeeklyTransactionAmountLimit { get; set; } = -1;
    }

    public class TrusteeInformation
    {
        public Guid Id { get; set; }
        public String UserId { get; set; }
        public Guid CreateTranId { get; set; }
        public Guid UpdateTranId { get; set; }
        public TrusteeApplicationStatus ApplicationStatus { get; set; }
        public String Remark { get; set; }
    }
    public class TrusteeBankAccount

    {
        public Guid TrusteId { get; set; }
        public String BankId { get; set; }
        public String AccountName { get; set; }
        public String AccountNumber { get; set; }
        public int Order { get; set; }
        public String Remark { get; set; }

    }
    public enum OfferStatus
    {
        None,
        Open,
        BuyerSetWaitingTrustee,
        TrusteeSet,
        PayClaimedByBuyer,
        PayConfirmedByTrustee,
        FulfilClaimed,
        FulfilConfirmed,
        PayClaimedByTrustee,
        PayConfirmedBySeller,
        BuyerPayDisputedByTrustee,
        FulfilDipustedByBuyer,
        PayDisputedBySeller,
    }

    public class ExchangeOffer
    {
        public Guid Id { get; set; }
        public Guid CreateTranId { get; set; }
        public Guid UpdateTranId { get; set; }
        public long Time { get; set; }
        public String AssetKey { get; set; }
        public long Price { get; set; }
        public long Amount { get; set; }
        public OfferStatus Status { get; set; }
        public long StatusTime { get; set; }
        [NotMapped]
        public List<OfferBankAccount> BankAccounts { get; set; }
    }
    
    public class OfferBankAccount
    {
        public Guid OfferId { get; set; }
        public String BankId { get; set; }
        public String AccountName { get; set; }
        public String AccountNumber { get; set; }
        public int Order { get; set; }
    }
    public class OfferStatusHistory
    {
        [Key]
        public Guid TranId { get; set; }
        public Guid? PrevTranId { get; set; }
        public int SeqNo { get; set; }
        public Guid OfferId { get; set; }      
        public OfferStatus OldStatus { get; set; }
        public OfferStatus Status { get; set; }
        public String UserId { get; set; }
        public String Remark { get; set; }
    }
    public class TrdAcceptOffer
    {
        public Guid OfferId { get; set; }
        public String BankId { get; set; }
        public String WalletAddress { get; set; }
    }
    public class TrdTakeTrusteeship
    {
        public Guid OfferId { get; set; }
    }
    public class TrdPayClaim
    {
        public Guid OfferId { get; set; }
        public List<ContentData> Data { get; set; }
    }
    public class TrdFulfilClaim
    {
        public Guid OfferId { get; set; }
        public List<ContentData> Data { get; set; }
    }
    public class ExchangeTransaction
    {
        public Guid Id { get; set; }
        public String UserId { get; set; }
        public long Time { get; set; }
        public String TransactionType { get; set; }
    }
    public class ExchangeTransactionData
    {
        [Key]
        public Guid TranId { get; set; }
        public String Data { get; set; }
        public Object DeserializedData(String TransactionType)
        {
                if (Data == null || TransactionType == null)
                    return null;
                var type = Type.GetType(TransactionType.Substring(ExchangeDbService.OBJECT_TYPE_PREFIX.Length));
                if (type == null)
                    return null;
                return Newtonsoft.Json.JsonConvert.DeserializeObject(this.Data, type);
        }
    }
    


    public enum TrusteeApplicationStatus
    {
        Apply,
        Approved,
        Canceled
    }
    public class TrusteeApplication
    {
        [Key]
        public Guid TranId { get; set; }
        public Guid UpdateTranId { get; set; }
        public String UserId { get; set; }
        public TrusteeApplicationStatus Status { get; set; }
    }
    public class TrdTrusteeApplication
    {
        public TrusteeInformation Trustee { get; set; }
        public List<TrusteeBankAccount> BankAccounts { get; set; }
    }
    public class TrdRejectTrusteeApplication
    {
        public String Reason { get; set; }
    }
}
