using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TgBot.Exchange
{
    public class ExchangeDbService : ServiceBase<ExchangeDb>
    {
        public const double MIN_OFFER_PRICE = 0.01;
        public const double MAX_OFFER_PRICE = 1e9;
        public const string OBJECT_TYPE_PREFIX = "Object_";
        protected delegate void ExchangeTransactNoReturnDelegate(ExchangeDb db, ExchangeTransaction tran);
        protected delegate T ExchangeTransactionReturnDelegate<T>(ExchangeDb db, ExchangeTransaction tran);
        private static ExchangeTransaction SaveExchangeTransaction(ExchangeDb db, string userId, string TranType, object Data)
        {
            var tran = new ExchangeTransaction();
            tran.Id = Guid.NewGuid();
            tran.Time = DateTime.Now.Ticks;
            tran.UserId = userId;
            String tranData = null;
            if (TranType == null && Data != null)
            {
                tran.TransactionType = OBJECT_TYPE_PREFIX + Data.GetType().ToString();
                tranData= Newtonsoft.Json.JsonConvert.SerializeObject(Data);
            }
            else if (TranType != null && Data == null)
            {
                if (TranType.StartsWith(OBJECT_TYPE_PREFIX))
                    throw new InvalidOperationException($"{OBJECT_TYPE_PREFIX} prefix not allowed for transaction");
                tran.TransactionType = TranType;
            }
            else
                throw new InvalidOperationException($"Invalid transaction type");
            db.ExchangeTransactions.Add(tran);
            if(tranData!=null)
            {
                db.ExchangeTransactionData.Add(
                    new ExchangeTransactionData
                    {
                        TranId=tran.Id,
                        Data=tranData,
                    }
                    );
            }
            return tran;
        }
        void ExchangeTransactionNoReturn(String userId, String TranType, Object Data, ExchangeTransactNoReturnDelegate f)
        {
            base.TransactNoReturn(db =>
            {
                ExchangeTransaction tran = SaveExchangeTransaction(db,userId, TranType, Data);
                db.ExchangeTransactions.Add(tran);
                f(db, tran);
            });
        }
        T ExchangeTransaction<T>(String userId, String TranType, Object Data, ExchangeTransactionReturnDelegate<T> f)
        {
            return base.TransactReturn(db =>
            {
                var tran = SaveExchangeTransaction(db,userId, TranType, Data);
                return f(db, tran);
            });
        }
        ExchangeUserProfile GetUserProfile(ExchangeDb db, String userId)
            => db.UserProfiles.Where(x => x.UserId == userId).FirstOrDefault();
        public ExchangeUserProfile GetUserProfile(String userId)
            => DbRead(db => db.UserProfiles.Where(x => x.UserId == userId).FirstOrDefault());

        TrusteeInformation GetTrusteeInformation(ExchangeDb db, String userId)
        {
            return db.TrusteeInformations.Where(x => x.UserId == userId).FirstOrDefault();
        }
        public void RegisterUser(ExchangeUserProfile userProfile)
        {
            ExchangeTransactionNoReturn(userProfile.UserId, null, userProfile, (db, t) =>
               {
                   if (GetUserProfile(db, userProfile.UserId) != null)
                       throw new InvalidOperationException("User already registerd");
                   userProfile.TranId = t.Id;
                   userProfile.EmailVarified = false;
                   userProfile.UserStatus = UserStatus.Approved;
                   db.UserProfiles.Add(userProfile);
               });
        }

        public void ApplyForTrustee(TrdTrusteeApplication application)
        {
            ExchangeTransactionNoReturn(application.Trustee.UserId, null, application, (db, tran) =>
            {
                AssertExistingUser(db, application.Trustee.UserId);
                var existing = GetTrusteeInformation(db, application.Trustee.UserId);
                if (existing != null)
                    throw new InvalidOperationException("User already registered as trustee");
                if (db.TrusteeApplications.Where(x => x.UserId == application.Trustee.UserId && x.Status == TrusteeApplicationStatus.Apply).Any())
                    throw new InvalidOperationException("User already have a pending application");
                var app = new TrusteeApplication()
                {
                    Status = TrusteeApplicationStatus.Apply,
                    TranId = tran.Id,
                    UpdateTranId = tran.Id,
                };
                foreach (var b in application.BankAccounts)
                    if (!db.BankAccountTypes.Where(x => x.BankId == b.BankId).Any())
                        throw new InvalidOperationException($"Invalid bank id:{b.BankId}");
                db.TrusteeApplications.Add(app);
            });
        }
        public void AcceptTrusteeApplication(String userId)
        {
            ExchangeTransactionNoReturn(userId, "AcceptTrusteeApplication", null, (db, tran) =>
            {
                AssertExistingUser(db, userId);
                var existing = GetTrusteeInformation(db, userId);
                if (existing != null)
                    throw new InvalidOperationException("User already registered as trustee");
                var application = db.TrusteeApplications.Where(x => x.UserId == userId && x.Status == TrusteeApplicationStatus.Apply).FirstOrDefault();
                if (application == null)
                    throw new InvalidOperationException("Application doesn't exist");
                application.Status = TrusteeApplicationStatus.Approved;
                application.UpdateTranId = tran.Id;
                db.TrusteeApplications.Update(application);
                var applyTran = GetTransactionData(db, db.ExchangeTransactions.Where(x => x.Id == application.TranId).First()) as TrdTrusteeApplication;
                applyTran.Trustee.Id = Guid.NewGuid();
                applyTran.Trustee.UserId = userId;
                applyTran.Trustee.CreateTranId = tran.Id;
                applyTran.Trustee.UpdateTranId = tran.Id;
                applyTran.Trustee.ApplicationStatus = TrusteeApplicationStatus.Approved;
                db.TrusteeInformations.Add(applyTran.Trustee);
                foreach (var b in applyTran.BankAccounts)
                {
                    b.TrusteId = applyTran.Trustee.Id;
                    db.TrusteeBankAccounts.Add(b);
                }
            });
        }

        internal void SetExchangeUserProfile(ExchangeUserProfile exchangeUserProfile)
        {
            ExchangeTransactionNoReturn(exchangeUserProfile.UserId, null, exchangeUserProfile, (db, tran) =>
            {
                exchangeUserProfile.TranId = tran.Id;
                db.UserProfiles.Add(exchangeUserProfile);
            });
        }

        public void RejectTrusteeApplication(String userId, string Reason)
        {
            ExchangeTransactionNoReturn(userId, null, new TrdRejectTrusteeApplication { Reason = Reason }, (db, tran) =>
               {
                   AssertExistingUser(db, userId);
                   var existing = GetTrusteeInformation(db, userId);
                   if (existing != null)
                       throw new InvalidOperationException("User already registered as trustee");
                   var application = db.TrusteeApplications.Where(x => x.UserId == userId && x.Status == TrusteeApplicationStatus.Apply).FirstOrDefault();
                   if (application == null)
                       throw new InvalidOperationException("Application doesn't exist");
                   application.Status = TrusteeApplicationStatus.Canceled;
                   application.UpdateTranId = tran.Id;
                   db.TrusteeApplications.Update(application);
               });
        }

        private ExchangeUserProfile AssertExistingUser(ExchangeDb db, string userId)
        {
            var user = GetUserProfile(db, userId);
            if (user == null)
                throw new InvalidOperationException("User not registered");
            return user;
        }
        void UpdateStatus(String userId, ExchangeDb db, ExchangeTransaction tran, ExchangeOffer existingOffer, OfferStatus newStatus, String remark)
        {
            existingOffer.Status = newStatus;
            var prevTran = existingOffer.UpdateTranId;
            var prevHistory = db.OfferStatusHistory.Where(x => x.OfferId == existingOffer.Id && x.TranId == prevTran).FirstOrDefault();
            existingOffer.UpdateTranId = tran.Id;
            db.ExchangeOffers.Update(existingOffer);
            
            db.OfferStatusHistory.Add(new OfferStatusHistory()
            {
                TranId = tran.Id,
                PrevTranId = prevHistory.TranId,
                SeqNo = prevHistory==null?1:prevHistory.SeqNo+1,
                OfferId = existingOffer.Id,
                OldStatus = OfferStatus.Open,
                Status = OfferStatus.BuyerSetWaitingTrustee,
                UserId = userId,
                Remark = remark,
            });
        }

        public void MakeExchangeOffer(String userId, ExchangeOffer offer)
        {
            ExchangeTransactionNoReturn(userId, null, offer, (db, tran) =>
               {
                   var asset = db.AssetTypes.Where(x => x.Key == offer.AssetKey).FirstOrDefault();
                   if (asset == null)
                       throw new InvalidOperationException($"Invalid asset type:{offer.AssetKey}");
                   var price = IntData.amountToDouble(offer.Price);
                   if (price < MIN_OFFER_PRICE || price > MAX_OFFER_PRICE)
                       throw new InvalidOperationException("Out of range price");
                   if (offer.Amount < asset.MinOffer || offer.Amount > asset.MinOffer)
                       throw new InvalidOperationException("Out of range offer amount");
                   offer.Id = Guid.NewGuid();
                   offer.CreateTranId = tran.Id;
                   offer.UpdateTranId = tran.Id;
                   offer.Time = DateTime.Now.Ticks;
                   offer.Status = OfferStatus.Open;
                   offer.StatusTime = offer.Time;
                   if (offer.BankAccounts == null || offer.BankAccounts.Count == 0)
                       throw new InvalidOperationException("At least one bank account should be specified for the offer.");
                   var trusteeExists = false;
                   foreach (var b in offer.BankAccounts)
                   {
                       if (!db.OfferBankAccounts.Where(x => x.BankId == b.BankId).Any())
                           throw new InvalidOperationException($"Invalid bank {b.BankId}");
                       if (db.TrusteeBankAccounts.Where(x => x.BankId == b.BankId).Any())
                           trusteeExists = true;
                   }
                   if (!trusteeExists)
                       throw new InvalidOperationException("Sorry no trustee found that accept your bank");
                   db.ExchangeOffers.Add(offer);
                   db.OfferStatusHistory.Add(new OfferStatusHistory()
                   {
                       TranId = tran.Id,
                       PrevTranId = null,
                       SeqNo = 1,
                       OfferId = offer.Id,
                       OldStatus = OfferStatus.None,
                       Status = OfferStatus.Open,
                       UserId = userId,
                       Remark = "",
                   });
               });
        }
        void TranversExchangeHistory(ExchangeDb db, ExchangeOffer offer, Func<OfferStatusHistory,bool> f)
        {
            Guid? tranId=offer.UpdateTranId;
            do
            {
                var h = db.OfferStatusHistory.Where(x => x.TranId == tranId).FirstOrDefault();
                if (f(h))
                    break;
                tranId = h.PrevTranId;
            } while (tranId != null);

        }
        public void AcceptExchangeOffer(String userId, TrdAcceptOffer acceptData)
        {
            ExchangeTransactionNoReturn(userId, null, acceptData, (db, tran) =>
               {

                   if (String.IsNullOrEmpty(acceptData.WalletAddress))
                       throw new InvalidOperationException("Wallet address can't be empty");

                   var offer = db.ExchangeOffers.Where(x => x.Id == acceptData.OfferId && x.Status == OfferStatus.Open).FirstOrDefault();
                   if (offer == null)
                       throw new InvalidOperationException($"Sell offer {acceptData.OfferId} doesn't exist");
                   var offeraccount = db.OfferBankAccounts.Where(x => x.OfferId == acceptData.OfferId);
                   var trustees = new List<Guid>();
                   foreach (var ta in db.TrusteeBankAccounts.Where(x => x.BankId == acceptData.BankId))
                   {
                       foreach (var oa in offeraccount)
                       {
                           if (db.TrusteeBankAccounts.Where(x => x.TrusteId == ta.TrusteId && x.BankId == oa.BankId).Any())
                           {
                               trustees.Add(ta.TrusteId);
                               break;

                           }
                       }
                   }
                   if (trustees.Count == 0)
                       throw new InvalidOperationException("Sorry, we can't transfer to this bank at this moment.");
                   UpdateStatus(userId, db, tran, offer, OfferStatus.BuyerSetWaitingTrustee, "");
               });
        }
        public void BecomeTrustee(String userId, TrdTakeTrusteeship data)
        {
            ExchangeTransactionNoReturn(userId, null, data, (db, tran) =>
               {
                   var offer = db.ExchangeOffers.Where(x => x.Id == data.OfferId && x.Status == OfferStatus.BuyerSetWaitingTrustee).FirstOrDefault();
                   if (offer == null)
                       throw new InvalidOperationException($"Accepted sell offer {data.OfferId} doesn't exist");

                   var acceptanceTran = db.ExchangeTransactions.Where(x => x.Id == offer.UpdateTranId).FirstOrDefault();
                   if (acceptanceTran == null)
                       throw new InvalidOperationException($"Buy transaction doesn't exist");
                   var buyInfo = GetTransactionData(db, acceptanceTran) as TrdAcceptOffer;
                   if (buyInfo == null)
                       throw new InvalidOperationException("Buyer information not found");
                   var trustee = db.TrusteeInformations.Where(x => x.UserId == userId).FirstOrDefault();
                   if (trustee == null)
                       throw new InvalidOperationException("User not trustee");
                   var trusteeBankAccounts = db.TrusteeBankAccounts.Where(x => x.TrusteId == trustee.Id).OrderBy(x => x.Order);
                   String offerBankAccount = null;
                   foreach (var ba in offer.BankAccounts)
                   {
                       if (trusteeBankAccounts.Where(x => x.BankId == ba.BankId).Any())
                       {
                           offerBankAccount = ba.BankId;
                           break;
                       }
                   }
                   if (offerBankAccount == null)
                       throw new InvalidCastException("Trustee doesn't support seller bank account");
                   if (!trusteeBankAccounts.Where(x => x.BankId == buyInfo.BankId).Any())
                       throw new InvalidCastException("Trustee doesn't support buyer bank account");
                   UpdateStatus(userId, db, tran, offer, OfferStatus.TrusteeSet, "");
               });
        }
        public void BuyerPaid(String userId, TrdPayClaim data)
        {
            ExchangeTransactionNoReturn(userId, null, data, (db, tran) =>
            {
                if (data.Data.Count == 0)
                    throw new InvalidOperationException("At least one evidence for payment should be attached");
                var offer = db.ExchangeOffers.Where(x => x.Id == data.OfferId && x.Status == OfferStatus.TrusteeSet).FirstOrDefault();
                if (offer == null)
                    throw new InvalidOperationException($"Sell offer {data.OfferId} doesn't exist");
                ExchangeTransaction tranTrustee = null, tranBuy = null;
                //the trustee set transaction that should be the last transaction
                tranTrustee = db.ExchangeTransactions.Where(x => x.Id == offer.UpdateTranId).FirstOrDefault();
                var prevSeqNo=db.OfferStatusHistory.Where(x => x.OfferId == data.OfferId && x.TranId == tranTrustee.Id).FirstOrDefault().SeqNo - 1;
                var h= db.OfferStatusHistory.Where(x => x.OfferId == data.OfferId && x.SeqNo == prevSeqNo).FirstOrDefault();
                if(h!=null)
                    tranBuy= db.ExchangeTransactions.Where(x => x.Id == h.TranId).FirstOrDefault();
                TrdAcceptOffer buyerInfo;
                TrdTakeTrusteeship trusteeInfo;
                if (tranTrustee == null || (trusteeInfo = GetTransactionData(db, tranTrustee) as TrdTakeTrusteeship) == null)
                    throw new InvalidOperationException($"Trustee information could't be found");
                if (tranBuy == null || (buyerInfo = GetTransactionData(db, tranTrustee) as TrdAcceptOffer) == null)
                    throw new InvalidOperationException($"Buyer information couldn't be found");
                if (tranBuy.UserId.Equals(userId))
                    throw new AccessViolationException($"Only the buyer can make the payment");
                UpdateStatus(userId, db, tran, offer, OfferStatus.PayClaimedByBuyer, "");
            });
        }
        public void BuyerPaidConfirmByTrustee(String userId,Guid offerId)
        {
            ExchangeTransactionNoReturn(userId, "BuyerPaidConfirm", null, (db, tran) =>
            {
                var offer = db.ExchangeOffers.Where(x => x.Id == offerId && x.Status == OfferStatus.PayClaimedByBuyer).FirstOrDefault();
                if (offer == null)
                    throw new InvalidOperationException($"Sell offer {offerId} doesn't exist");
                ExchangeTransaction trasteeTran = null;
                TranversExchangeHistory(db, offer, x =>
                {
                    if(x.Status==OfferStatus.TrusteeSet)
                    {
                        trasteeTran = db.ExchangeTransactions.Where(y => y.Id == x.TranId).FirstOrDefault();
                        return true;
                    }
                    return false;
                });
                
                if (trasteeTran == null)
                    throw new InvalidOperationException("Trustee not found");
                if(!trasteeTran.UserId.Equals(userId))
                    throw new AccessViolationException($"Only assigned trustee can confirm payment");
                UpdateStatus(userId, db, tran, offer, OfferStatus.PayConfirmedByTrustee, "");
            });
        }
        public void SellerClaimedFulfil(String userId, TrdFulfilClaim data)
        {
            ExchangeTransactionNoReturn(userId, null, data, (db, tran) =>
            {
                if (data.Data.Count == 0)
                    throw new InvalidOperationException("At least one evidence for payment should be attached");
                var offer = db.ExchangeOffers.Where(x => x.Id == data.OfferId && x.Status == OfferStatus.PayConfirmedByTrustee).FirstOrDefault();
                if (offer == null)
                    throw new InvalidOperationException($"Sell offer {data.OfferId} doesn't exist");
                ExchangeTransaction prevTran = null;
                
                //the trustee set transaction that should be the last transaction
                prevTran = db.ExchangeTransactions.Where(x => x.Id == offer.UpdateTranId).FirstOrDefault();
                if (prevTran == null)
                    throw new InvalidOperationException($"Trustee payment confirmation information could't be found");
                var offerTran = db.ExchangeTransactions.Where(x => x.Id == offer.CreateTranId).FirstOrDefault();
                if (!offerTran.UserId.Equals(userId))
                    throw new InvalidOperationException("Only the seller can claim asset transfer");
                UpdateStatus(userId, db, tran, offer, OfferStatus.FulfilClaimed, "");
            });
        }
        public void AssetTransferConfirmed(string userId, Guid offerId)
        {
            ExchangeTransactionNoReturn(userId, "AssetTransferConfirmed", null, (db, tran) =>
            {
                var offer = db.ExchangeOffers.Where(x => x.Id == offerId && x.Status == OfferStatus.FulfilClaimed).FirstOrDefault();
                if (offer == null)
                    throw new InvalidOperationException($"Sell offer {offerId} doesn't exist");
                OfferStatusHistory buyerH = null;
                OfferStatusHistory trusteH = null;
                TranversExchangeHistory(db, offer, x =>
                {
                    if(x.Status==OfferStatus.BuyerSetWaitingTrustee)
                    {
                        buyerH = x;
                        return true;
                    }
                    if(x.Status==OfferStatus.TrusteeSet)
                    {
                        trusteH = x;
                    }
                    return false;
                });
                if (buyerH == null)
                    throw new InvalidOperationException("Buyer transaction couldn't be found");
                if (trusteH== null)
                    throw new InvalidOperationException("Trauste transaction couldn't be found");
                ExchangeTransaction buyTran = db.ExchangeTransactions.Where(x => x.Id==buyerH.TranId).FirstOrDefault();
                if (buyTran == null)
                    throw new InvalidOperationException("Offer transaction not found");
                ExchangeTransaction trusteTran = db.ExchangeTransactions.Where(x => x.Id == trusteH.TranId).FirstOrDefault();
                if (trusteTran == null)
                    throw new InvalidOperationException("Offer transaction not found");
                if (!buyTran.UserId.Equals(userId) && !trusteTran.UserId.Equals(userId))
                    throw new AccessViolationException($"Only buyer or trustee can confirm asset transfer");
                UpdateStatus(userId, db, tran, offer, OfferStatus.FulfilConfirmed, "");
            });
        }
        public void TrusteeClaimedPay(String userId, TrdPayClaim data)
        {
            ExchangeTransactionNoReturn(userId, null, data, (db, tran) =>
            {
                if (data.Data.Count == 0)
                    throw new InvalidOperationException("At least one evidence for payment should be attached");
                var offer = db.ExchangeOffers.Where(x => x.Id == data.OfferId && x.Status == OfferStatus.FulfilConfirmed
                    ).FirstOrDefault();
                if (offer == null)
                    throw new InvalidOperationException($"Sell offer {data.OfferId} doesn't exist");
                ExchangeTransaction prevTran = null;
                //the trustee set transaction that should be the last transaction
                prevTran = db.ExchangeTransactions.Where(x => x.Id == offer.UpdateTranId).FirstOrDefault();
                if (prevTran == null)
                    throw new InvalidOperationException($"Assset transfer transaction confirmation information could't be found");
                if (!prevTran.UserId.Equals(userId))
                    throw new InvalidOperationException("Only the assigned trustee can claim payment to seller");
                UpdateStatus(userId, db, tran, offer, OfferStatus.PayClaimedByTrustee, "");
            });
        }
        public void PaymentConfirmedBySeller(string userId, Guid offerId)
        {
            ExchangeTransactionNoReturn(userId, "PaymentConfirmedBySeller", null, (db, tran) =>
            {
                var offer = db.ExchangeOffers.Where(x => x.Id == offerId && x.Status == OfferStatus.PayClaimedByTrustee).FirstOrDefault();
                if (offer == null)
                    throw new InvalidOperationException($"Sell offer {offerId} doesn't exist");
                ExchangeTransaction offerTran = db.ExchangeTransactions.Where(x=>x.Id==offer.CreateTranId).FirstOrDefault();

                if (offerTran == null)
                    throw new InvalidOperationException("Offer transaction not found");
                if (!offerTran.UserId.Equals(userId))
                    throw new AccessViolationException($"Only seller can confirm payment");
                UpdateStatus(userId, db, tran, offer, OfferStatus.PayConfirmedBySeller, "");
            });
        }
        public void TransferAssetClaimedBySeller(String userid,TrdPayClaim data)
        {

        }
        public Object GetTransactionData(ExchangeDb db, ExchangeTransaction tran)
        {
            var td = db.ExchangeTransactionData.Where(x => x.TranId == tran.Id).FirstOrDefault();
            if (td == null)
                return null;
            return td.DeserializedData(tran.TransactionType);
        }
    }
}
